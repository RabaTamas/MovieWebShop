import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// Külön konfiguráció: a vite.config.js fejlesztői módban HTTPS-tanúsítványt generál
// és PWA-t épít, amire a tesztekhez nincs szükség.
export default defineConfig({
    plugins: [react()],
    resolve: {
        alias: {
            "@": fileURLToPath(new URL("./src", import.meta.url)),
            // A vite-plugin-pwa virtuális modulja csak a PWA-pluginnal létezik
            "virtual:pwa-register/react": fileURLToPath(new URL("./src/test/pwaRegister.js", import.meta.url)),
        },
    },
    test: {
        environment: "jsdom",
        globals: true,
        setupFiles: ["./src/test/setup.js"],
        include: ["src/**/*.test.{js,jsx}"],
        css: false,
        // A teljes oldalakat végigjáró tesztek párhuzamos futásnál, terhelt gépen lassabbak
        testTimeout: 60000,
        coverage: {
            provider: "v8",
            include: ["src/**/*.{js,jsx}"],
            // Kizárva: belépési pont, service worker (böngészőn kívül nem fut),
            // és a shadcn/ui-ból generált, változatlan UI-primitívek
            exclude: ["src/test/**", "src/**/*.test.{js,jsx}", "src/main.jsx", "src/sw.js", "src/components/ui/**"],
        },
    },
});
