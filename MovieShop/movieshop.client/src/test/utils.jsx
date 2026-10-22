import { render } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";

import { AuthProvider } from "@/contexts/AuthContext";
import { ThemeProvider } from "@/contexts/ThemeContext";

/** Aláírás nélküli JWT a megadott claimekkel — a kliens csak dekódolja, nem ellenőrzi. */
export function makeJwt(claims, { expiresInSeconds = 3600 } = {}) {
    const encode = (obj) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
    const payload = { exp: Math.floor(Date.now() / 1000) + expiresInSeconds, ...claims };
    return `${encode({ alg: "HS256", typ: "JWT" })}.${encode(payload)}.signature`;
}

/** Az aktuális útvonalat kiíró segédkomponens az átirányítások ellenőrzéséhez. */
export function LocationDisplay() {
    const location = useLocation();
    return <div data-testid="location">{location.pathname}</div>;
}

/**
 * Komponens renderelése a valódi Auth- és Theme-providerrel, egy memóriabeli routerben.
 * `routes`: további útvonalak (pl. /login), hogy az átirányítások célja is látszódjon.
 */
export function renderWithProviders(ui, { route = "/", path = "*", routes = null } = {}) {
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
