import { expect, test } from "@playwright/test";

import { ADMIN, bearer, login } from "../helpers.js";

test.describe("API Gateway", () => {
    test("a nyilvános katalógus token nélkül elérhető", async ({ request }) => {
        const res = await request.get("/api/Movie");

        expect(res.status()).toBe(200);
        const movies = await res.json();
        expect(Array.isArray(movies)).toBe(true);
        expect(movies.length).toBeGreaterThan(0);
        expect(movies[0]).toHaveProperty("title");
    });

    test("minden szolgáltatáshoz a gatewayen át lehet eljutni", async ({ request }) => {
        const { token } = await login(request, ADMIN.email, ADMIN.password);

        const checks = {
            "/api/Category": 200,                                   // catalog
            "/api/Auction": 200,                                    // auction
            "/api/User/profile": 200,                               // user
            "/api/ShoppingCart": 200,                               // order
            "/api/Recommendation": 200,                             // order
        };

        for (const [path, expected] of Object.entries(checks)) {
            const res = await request.get(path, { headers: bearer(token) });
            expect(res.status(), path).toBe(expected);
        }
    });

    test("a belső (szolgáltatások közötti) végpontok kívülről nem érhetők el", async ({ request }) => {
        for (const path of ["/api/internal/catalog/movies", "/api/internal/users/republish"]) {
            const res = await request.get(path);
            expect(res.status(), path).toBe(404);
        }
    });

    test("védett végpont token nélkül 401-et ad", async ({ request }) => {
        for (const path of ["/api/ShoppingCart", "/api/Order/user", "/api/Movie/purchased"]) {
            const res = await request.get(path);
            expect(res.status(), path).toBe(401);
        }
    });

    test("admin végpont sima felhasználónak 403-at ad", async ({ request }) => {
        const email = `e2e_role_${Date.now()}@movieshop.test`;
        const reg = await request.post("/api/Auth/register", {
            data: { name: "e2e_role", email, password: "E2e-Test123!", confirmPassword: "E2e-Test123!" },
        });
        const { token, user } = await reg.json();

        const res = await request.get("/api/admin/Orders", { headers: bearer(token) });
        expect(res.status()).toBe(403);

        const admin = await login(request, ADMIN.email, ADMIN.password);
        await request.delete(`/api/User/${user.id}`, { headers: bearer(admin.token) });
    });

    test("az egyik szolgáltatás által kiadott JWT-t a többi is elfogadja", async ({ request }) => {
        // A tokent kizárólag a User Service állítja ki
        const { token } = await login(request, ADMIN.email, ADMIN.password);

        // Három különböző szolgáltatás admin-végpontja: catalog, order, auction
        for (const path of ["/api/Movie/admin/all", "/api/admin/Orders", "/api/Auction/all"]) {
            const res = await request.get(path, { headers: bearer(token) });
            expect(res.status(), path).toBe(200);
        }
    });
});
