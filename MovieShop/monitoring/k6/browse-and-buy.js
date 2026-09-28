// Terhelésteszt a MovieShop mikroszervíz stackhez (k6).
//
// Egy virtuális felhasználó azt csinálja, amit egy valódi: megnyitja a főoldalt,
// belenéz egy filmbe, keres, bejelentkezik, kosárba tesz. A lépések között
// "gondolkodási idő" van, különben nem terhelést mérnénk, hanem a hálózat
// maximumát.
//
// Futtatás (a monitoring/k6 mappából):
//   k6 run browse-and-buy.js
//   k6 run -e BASE=http://localhost:5200 -e VUS=50 browse-and-buy.js
//
// Szándékosan nem 500 virtuális felhasználóval dolgozunk: a k6 ugyanazon a gépen
// fut, mint a mért rendszer, így a túl nagy szám a saját processzorunkat mérné.
import http from "k6/http";
import { check, sleep, group } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE = __ENV.BASE || "http://localhost:5200";
const VUS = Number(__ENV.VUS || 30);
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || "admin@movieshop.com";
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || "Admin123!";

const hibaarany = new Rate("uzleti_hibak");
const bejelentkezesIdo = new Trend("bejelentkezes_ido", true);

export const options = {
    stages: [
        { duration: "1m", target: Math.round(VUS / 2) }, // felfutás
        { duration: "3m", target: VUS },                 // tartás
        { duration: "1m", target: 0 },                   // leállás
    ],
    thresholds: {
        // A kérések 95%-a 800 ms alatt, és 1% alatti hibaarány
        http_req_duration: ["p(95)<800"],
        http_req_failed: ["rate<0.01"],
        uzleti_hibak: ["rate<0.01"],
    },
};

function jo(res, nev) {
    const ok = check(res, { [`${nev}: 2xx`]: (r) => r.status >= 200 && r.status < 300 });
    hibaarany.add(!ok);
    return ok;
}

export function setup() {
    // Egy bejelentkezés a teszt elején: a token minden virtuális felhasználónak jó,
    // így nem a bejelentkezést terheljük feleslegesen.
    const res = http.post(`${BASE}/api/Auth/login`,
        JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
        { headers: { "Content-Type": "application/json" } });
    bejelentkezesIdo.add(res.timings.duration);
    return { token: res.json("token") };
}

export default function (data) {
    const auth = data.token ? { Authorization: `Bearer ${data.token}` } : {};

    group("fooldal", () => {
        jo(http.get(`${BASE}/api/Movie`, { tags: { nev: "filmlista" } }), "filmlista");
        jo(http.get(`${BASE}/api/Category`, { tags: { nev: "kategoriak" } }), "kategoriak");
    });
    sleep(Math.random() * 2 + 1);

    group("bongeszes", () => {
        const id = Math.floor(Math.random() * 3) + 1;
        jo(http.get(`${BASE}/api/Movie/${id}`, { tags: { nev: "filmadatlap" } }), "filmadatlap");
        jo(http.get(`${BASE}/api/Search?q=the`, { tags: { nev: "kereses" } }), "kereses");
    });
    sleep(Math.random() * 3 + 1);

    group("aukciok", () => {
        jo(http.get(`${BASE}/api/Auction`, { tags: { nev: "aukciok" } }), "aukciok");
    });
    sleep(Math.random() * 2 + 1);

    if (data.token) {
        group("kosar", () => {
            const res = http.get(`${BASE}/api/Cart`, { headers: auth, tags: { nev: "kosar" } });
            jo(res, "kosar");
        });
        sleep(1);
    }
}

export function handleSummary(data) {
    const m = data.metrics;
    const sor = (nev, ertek, mertek = "ms") =>
        `  ${nev.padEnd(34)} ${String(ertek).padStart(8)} ${mertek}`;

    const osszegzes = [
        "",
        "=== MovieShop terhelésteszt összegzés ===",
        sor("kérések összesen", m.http_reqs?.values?.count ?? 0, "db"),
        sor("kérés / másodperc", (m.http_reqs?.values?.rate ?? 0).toFixed(1), "req/s"),
        sor("válaszidő átlag", (m.http_req_duration?.values?.avg ?? 0).toFixed(0)),
        sor("válaszidő p95", (m.http_req_duration?.values?.["p(95)"] ?? 0).toFixed(0)),
        sor("válaszidő p99", (m.http_req_duration?.values?.["p(99)"] ?? 0).toFixed(0)),
        sor("hibaarány", ((m.http_req_failed?.values?.rate ?? 0) * 100).toFixed(2), "%"),
        "",
    ].join("\n");

    return {
        stdout: osszegzes,
        "eredmeny.json": JSON.stringify(data, null, 2),
    };
}
