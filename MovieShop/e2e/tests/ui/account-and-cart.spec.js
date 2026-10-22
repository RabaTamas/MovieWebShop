import { expect, test } from "@playwright/test";

import { ADMIN, PASSWORD, apiClient, deleteUser, login, uniqueEmail } from "../helpers.js";

/** A UI-n keresztül regisztrált felhasználók, a végén az admin törli őket. */
const createdEmails = [];

test.afterAll(async () => {
    const api = await apiClient();
    const { token } = await login(api, ADMIN.email, ADMIN.password);
    for (const email of createdEmails) {
        const res = await api.post("/api/Auth/login", { data: { email, password: PASSWORD } });
        if (res.ok()) await deleteUser(api, token, (await res.json()).user.id);
    }
    await api.dispose();
});

async function registerViaUi(page, name = "E2ETester") {
    const email = uniqueEmail("e2e_ui");
    createdEmails.push(email);

    await page.goto("/register");
    await page.getByLabel("Name").fill(name);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByLabel("Confirm Password").fill(PASSWORD);
    await page.getByRole("button", { name: "Register" }).click();

    await expect(page).toHaveURL(/\/$/);
    return email;
}

async function loginViaUi(page, email, password) {
    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: "Login", exact: true }).click();
}

test.describe("Fiók", () => {
    test("regisztráció után a felhasználó be van jelentkezve", async ({ page }) => {
        await registerViaUi(page, "E2ERegister");

        await expect(page.getByRole("button", { name: "Settings" })).toBeVisible();
        await expect(page.getByRole("link", { name: "Login" })).toHaveCount(0);
    });

    // ISMERT HIBA: az űrlap „Name” mezőt kér, a szerver viszont felhasználónévként menti,
    // amiben az ASP.NET Identity alapbeállítása nem enged szóközt. A felhasználó ilyenkor
    // a technikai „Username ... is invalid” üzenetet kapja. Javítás után a fixme törölhető.
    test.fixme("teljes név (szóközzel) is megadható regisztrációkor", async ({ page }) => {
        await registerViaUi(page, "Teszt Elek");
        await expect(page.getByRole("button", { name: "Settings" })).toBeVisible();
    });

    test("eltérő jelszavaknál a regisztráció nem megy át", async ({ page }) => {
        await page.goto("/register");
        await page.getByLabel("Name").fill("Mismatch");
        await page.getByLabel("Email").fill(uniqueEmail("e2e_mismatch"));
        await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
        await page.getByLabel("Confirm Password").fill(PASSWORD + "x");
        await page.getByRole("button", { name: "Register" }).click();

        await expect(page.getByText("Passwords do not match")).toBeVisible();
        await expect(page).toHaveURL(/\/register$/);
    });

    test("hibás jelszóval a belépés hibaüzenetet ad", async ({ page }) => {
        await loginViaUi(page, ADMIN.email, "Wrong-Password1!");

        await expect(page.getByText("Invalid email or password")).toBeVisible();
        await expect(page).toHaveURL(/\/login$/);
    });

    test("kijelentkezés után a védett oldal újra a loginra irányít", async ({ page }) => {
        await registerViaUi(page, "E2ELogout");

        await page.getByRole("button", { name: "Settings" }).click();
        await page.getByRole("menuitem", { name: "Logout" }).click();
        await page.goto("/orders");

        await expect(page).toHaveURL(/\/login$/);
    });

    test("sima felhasználó nem éri el az admin felületet", async ({ page }) => {
        await registerViaUi(page, "E2ENoAdmin");

        await page.goto("/admin/movies");

        await expect(page).toHaveURL(/\/unauthorized$/);
        await expect(page.getByText("403")).toBeVisible();
    });

    test("az admin eléri a rendeléskezelő felületet", async ({ page }) => {
        await loginViaUi(page, ADMIN.email, ADMIN.password);
        await expect(page).toHaveURL(/\/$/);

        await page.goto("/admin/orders");

        await expect(page).toHaveURL(/\/admin\/orders$/);
        await expect(page.getByText("403")).toHaveCount(0);
    });
});

test.describe("Kosár", () => {
    test("filmet kosárba tesz, majd eltávolítja", async ({ page }) => {
        await registerViaUi(page, "E2ECart");

        await page.goto("/");
        const firstCard = page.locator('a[href^="/movies/"]').first();
        const title = await firstCard.locator("h3").innerText();
        await firstCard.click();
        await page.getByRole("button", { name: "Add to Cart" }).click();

        await expect(page).toHaveURL(/\/cart$/);
        await expect(page.getByText(title).first()).toBeVisible();
        await expect(page.getByText("1 item in your cart")).toBeVisible();

        await page.getByRole("button", { name: "Remove" }).first().click();

        await expect(page.getByText("Your cart is empty.")).toBeVisible();
    });
});
