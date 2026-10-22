import { expect, test } from "@playwright/test";

test.describe("Böngészés vendégként", () => {
    test("a főoldal filmkártyákat jelenít meg", async ({ page }) => {
        await page.goto("/");

        const cards = page.locator('a[href^="/movies/"]');
        await expect(cards.first()).toBeVisible();
        expect(await cards.count()).toBeGreaterThan(0);
    });

    test("a keresés szűri a találatokat", async ({ page }) => {
        await page.goto("/");

        await page.getByRole("textbox", { name: "Search" }).fill("Matrix");

        // Az Elasticsearch-keresés debounce után indul; terhelt gépen lassabb lehet
        await expect(page.getByText(/\d+ results? found/)).toBeVisible({ timeout: 30_000 });
        await expect(page.getByRole("heading", { name: "The Matrix" }).first()).toBeVisible();
        await expect(page.getByRole("heading", { name: "Inception", exact: true })).toHaveCount(0);
    });

    test("a filmkártyáról a film adatlapjára jut", async ({ page }) => {
        await page.goto("/");

        const firstCard = page.locator('a[href^="/movies/"]').first();
        const title = await firstCard.locator("h3").innerText();
        await firstCard.click();

        await expect(page).toHaveURL(/\/movies\/\d+$/);
        await expect(page.getByRole("heading", { name: title }).first()).toBeVisible();
        await expect(page.getByRole("button", { name: "Add to Cart" })).toBeVisible();
    });

    test("vendég a kosár gombra kattintva a login oldalra kerül", async ({ page }) => {
        await page.goto("/");
        await page.locator('a[href^="/movies/"]').first().click();

        await page.getByRole("button", { name: "Add to Cart" }).click();

        await expect(page).toHaveURL(/\/login$/);
    });

    test("védett oldal bejelentkezés nélkül a login oldalra irányít", async ({ page }) => {
        await page.goto("/orders");

        await expect(page).toHaveURL(/\/login$/);
        await expect(page.getByRole("heading", { name: "Login" })).toBeVisible();
    });

    test("ismeretlen útvonal a 404-es oldalt mutatja", async ({ page }) => {
        await page.goto("/this-page-does-not-exist");

        await expect(page.getByText("404")).toBeVisible();
    });
});
