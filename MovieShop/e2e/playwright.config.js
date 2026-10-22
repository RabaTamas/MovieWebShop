import { defineConfig } from "@playwright/test";

// A tesztek a futó mikroszervizes stack ellen mennek:
//   docker compose -f docker-compose.microservices.yml up -d
// Felhős példány ellen: E2E_BASE_URL és E2E_API_URL környezeti változóval.
export const BASE_URL = process.env.E2E_BASE_URL ?? "http://localhost:3001";
export const API_URL = process.env.E2E_API_URL ?? "http://localhost:5200";

export default defineConfig({
    testDir: "./tests",
    timeout: 120_000,
    expect: { timeout: 30_000 },
    fullyParallel: false,
    workers: 1,
    retries: process.env.CI ? 1 : 0,
    reporter: [["list"], ["html", { open: "never" }]],
    use: {
        baseURL: BASE_URL,
        trace: "retain-on-failure",
        screenshot: "only-on-failure",
    },
    projects: [
        {
            name: "api",
            testMatch: /api\/.*\.spec\.js/,
            use: { baseURL: API_URL },
        },
        {
            name: "ui",
            testMatch: /ui\/.*\.spec\.js/,
            // A gépen lévő Chrome-ot használja, nem kell külön böngészőt letölteni.
            // CI-ben: npx playwright install chromium, és E2E_BROWSER_CHANNEL=chromium
            use: { channel: process.env.E2E_BROWSER_CHANNEL ?? "chrome", viewport: { width: 1400, height: 900 } },
        },
    ],
});
