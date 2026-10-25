import { render } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";

import { AuthProvider, useAuth } from "@/contexts/AuthContext";
import { ThemeProvider } from "@/contexts/ThemeContext";

/** Aláírás nélküli JWT a megadott claimekkel — a kliens csak dekódolja, nem ellenőrzi. */
export function makeJwt(claims, { expiresInSeconds = 3600 } = {}) {
    const encode = (obj) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
    const payload = { exp: Math.floor(Date.now() / 1000) + expiresInSeconds, ...claims };
    return `${encode({ alg: "HS256", typ: "JWT" })}.${encode(payload)}.signature`;
}

/** Bejelentkezett felhasználó szimulálása: a token a localStorage-ba kerül, az AuthProvider onnan tölti be. */
export function loginAs({ id = 1, name = "neo", email = "neo@zion.io", role = "User" } = {}) {
    const token = makeJwt({ nameid: String(id), name, email, role });
    localStorage.setItem("jwt", token);
    return token;
}

function makeResponse(status, body, contentType = "application/json") {
    const text = body === undefined ? "" : typeof body === "string" ? body : JSON.stringify(body);
    return {
        ok: status >= 200 && status < 300,
        status,
        statusText: String(status),
        headers: { get: (name) => (name.toLowerCase() === "content-type" ? contentType : null) },
        json: async () => (typeof body === "string" ? JSON.parse(body) : body),
        text: async () => text,
        blob: async () => new Blob([text]),
    };
}

/**
 * A `fetch` helyettesítése útvonal → válasz táblával.
 * Kulcs: "METÓDUS /útvonal" — pontos egyezés az útvonalra (query nélkül), vagy
 * `*` végződéssel prefix. Érték: a válasz törzse; `{ status, body, contentType }` (ha `status`
 * szám és nincs más kulcs); függvény `(req) => érték`; vagy Error (hálózati hiba).
 * A nem illeszkedő kérés 404-et kap. A hívások a `calls` tömbben vannak, a törzs JSON-ként dekódolva.
 */
export function mockApi(routes = {}) {
    const calls = [];
    const fetchMock = vi.fn(async (input, options = {}) => {
        const url = new URL(typeof input === "string" ? input : input.url, "http://localhost");
        const method = (options.method || "GET").toUpperCase();
        let body = options.body;
        if (typeof body === "string") {
            try {
                body = JSON.parse(body);
            } catch {
                /* nem JSON törzs */
            }
        }
        const req = { method, path: url.pathname, query: url.searchParams, body, headers: options.headers || {} };
        calls.push(req);

        const key = Object.keys(api.routes).find((k) => {
            const [m, p] = k.split(" ");
            if (m !== method) return false;
            if (p.includes("?")) return p === url.pathname + url.search;
            return p.endsWith("*") ? url.pathname.startsWith(p.slice(0, -1)) : p === url.pathname;
        });
        if (!key) return makeResponse(404, { message: "not mocked" });

        let value = api.routes[key];
        if (typeof value === "function") value = await value(req);
        if (value instanceof Error) throw value;
        if (value && typeof value === "object" && typeof value.status === "number" &&
            Object.keys(value).every((k) => ["status", "body", "contentType"].includes(k))) {
            return makeResponse(value.status, value.body, value.contentType);
        }
        return makeResponse(200, value);
    });
    const api = {
        routes,
        calls,
        fetch: fetchMock,
        /** Az adott metódusú és útvonalú (prefix) hívások. */
        find: (method, pathPrefix) => calls.filter((c) => c.method === method && c.path.startsWith(pathPrefix)),
    };
    vi.stubGlobal("fetch", fetchMock);
    return api;
}

/** Az aktuális útvonalat kiíró segédkomponens az átirányítások ellenőrzéséhez. */
export function LocationDisplay() {
    const location = useLocation();
    return <div data-testid="location">{location.pathname}</div>;
}

/** Az alkalmazás útvonalvédőihez hasonlóan csak a token betöltése után rendereli a tartalmat. */
function AuthReady({ children }) {
    const { loading } = useAuth();
    return loading ? null : children;
}

/**
 * Komponens renderelése a valódi Auth- és Theme-providerrel, egy memóriabeli routerben.
 * `routes`: további útvonalak (pl. /login), hogy az átirányítások célja is látszódjon.
 * `waitForAuth`: a PrivateRoute/AdminRoute mintájára megvárja a token betöltését (alapértelmezés).
 */
export function renderWithProviders(ui, { route = "/", path = "*", routes = null, waitForAuth = true } = {}) {
    if (waitForAuth) ui = <AuthReady>{ui}</AuthReady>;
    return render(
        <ThemeProvider>
            <AuthProvider>
                <MemoryRouter initialEntries={[route]}>
                    <Routes>
                        <Route path={path} element={ui} />
                        {routes}
                    </Routes>
                    <LocationDisplay />
                </MemoryRouter>
            </AuthProvider>
        </ThemeProvider>
    );
}
