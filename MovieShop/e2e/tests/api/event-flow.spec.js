import { expect, test } from "@playwright/test";

import { ADMIN, bearer, deleteUser, eventually, login, registerUser } from "../helpers.js";

/**
 * A szolgáltatások közötti aszinkron, eseményvezérelt adatreplikáció végpontok közötti
 * ellenőrzése. Minden lépés csak akkor sikerülhet, ha a megfelelő esemény
 * végigment a RabbitMQ-n és a fogadó szolgáltatás feldolgozta.
 */
test.describe("Eseményvezérelt replikáció", () => {
    let adminToken;

    test.beforeAll(async ({ request }) => {
        adminToken = (await login(request, ADMIN.email, ADMIN.password)).token;
    });

    test("MovieChanged: a Catalogban létrehozott film kosárba tehető az Order Service-ben", async ({ request }) => {
        const create = await request.post("/api/Movie", {
            headers: bearer(adminToken),
            data: {
                title: `E2E Event Flow ${Date.now()}`,
                description: "MovieChanged replikáció",
                price: 1500,
                imageUrl: "https://example.com/e2e.jpg",
                categories: [],
            },
        });
        const movieId = (await create.json()).id;

        try {
            expect(create.status()).toBe(201);

            // Catalog → MovieChanged → RabbitMQ → Order (MovieSnapshot)
            await eventually(
                async () => {
                    const res = await request.post("/api/ShoppingCart/add", {
                        headers: bearer(adminToken),
                        data: { movieId, quantity: 1 },
                    });
                    return res.ok();
                },
                { message: "a MovieChanged esemény nem ért el az Order Service-hez" }
            );

            const cart = await (await request.get("/api/ShoppingCart", { headers: bearer(adminToken) })).json();
            expect(cart.items.map((i) => i.movieId)).toContain(movieId);
        } finally {
            await request.delete(`/api/ShoppingCart/remove/${movieId}`, { headers: bearer(adminToken) });
            await request.delete(`/api/Movie/${movieId}`, { headers: bearer(adminToken) });
        }
    });

    test("OrderCompleted / OrderRevoked: a rendelés állapota szabályozza a streamelési jogosultságot", async ({ request }) => {
        const buyer = await registerUser(request, "e2e_buyer");
        const auth = bearer(buyer.token);
        const movieId = (await (await request.get("/api/Movie")).json())[0].id;

        const purchased = async () => {
            const res = await request.get("/api/Movie/purchased", { headers: auth });
            return (await res.json()).map((m) => m.id);
        };

        try {
            expect((await request.post("/api/ShoppingCart/add", { headers: auth, data: { movieId, quantity: 1 } })).ok()).toBe(true);

            // Fizetés nélküli rendelés: Pending állapotban jön létre, jogosultság még nincs
            const orderRes = await request.post("/api/Order", {
                headers: auth,
                data: { billingAddress: { street: "E2E utca 1.", city: "Budapest", zip: "1111" }, movies: [], totalPrice: 0 },
            });
            expect(orderRes.status()).toBe(200);
            const order = await orderRes.json();
            expect(order.status).toBe("Pending");
            expect(await purchased()).not.toContain(movieId);

            // A rendelés a kosarat kiürítette, a számlázási cím a User Service-ben is megjelent
            const cart = await (await request.get("/api/ShoppingCart", { headers: auth })).json();
            expect(cart.items).toHaveLength(0);
            const addresses = await (await request.get("/api/Address", { headers: auth })).json();
            expect(addresses.map((a) => a.street)).toContain("E2E utca 1.");

            // Order → OrderCompleted → RabbitMQ → Catalog (Entitlements)
            await request.put(`/api/admin/Orders/${order.id}/status`, { headers: bearer(adminToken), data: { status: "Completed" } });
            await eventually(async () => (await purchased()).includes(movieId), {
                message: "az OrderCompleted után a film nem lett streamelhető",
            });

            // Order → OrderRevoked → RabbitMQ → Catalog
            await request.put(`/api/admin/Orders/${order.id}/status`, { headers: bearer(adminToken), data: { status: "Refunded" } });
            await eventually(async () => !(await purchased()).includes(movieId), {
                message: "az OrderRevoked után a hozzáférés megmaradt",
            });
        } finally {
            await deleteUser(request, adminToken, buyer.id);
        }
    });

    test("UserChanged: a törölt felhasználó rendelései az Order Service-ből is eltűnnek", async ({ request }) => {
        const user = await registerUser(request, "e2e_deleted");
        const auth = bearer(user.token);
        const movieId = (await (await request.get("/api/Movie")).json())[0].id;

        await request.post("/api/ShoppingCart/add", { headers: auth, data: { movieId, quantity: 1 } });
        const order = await (
            await request.post("/api/Order", {
                headers: auth,
                data: { billingAddress: { street: "Törlés utca 2.", city: "Budapest", zip: "1111" }, movies: [], totalPrice: 0 },
            })
        ).json();

        const orderIds = async () =>
            (await (await request.get("/api/admin/Orders", { headers: bearer(adminToken) })).json()).map((o) => o.id);
        expect(await orderIds()).toContain(order.id);

        const del = await request.delete(`/api/User/${user.id}`, { headers: bearer(adminToken) });
        expect(del.status()).toBe(204);

        // User → UserChanged(IsDeleted) → RabbitMQ → Order
        await eventually(async () => !(await orderIds()).includes(order.id), {
            message: "a UserChanged törlési esemény nem takarította ki a rendelést",
        });
    });
});
