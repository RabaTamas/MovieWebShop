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
        },
    },
    test: {
        environment: "jsdom",
        globals: true,
        setupFiles: ["./src/test/setup.js"],
        include: ["src/**/*.test.{js,jsx}"],
        css: false,
        coverage: {
            provider: "v8",
            include: ["src/**/*.{js,jsx}"],
            exclude: ["src/test/**", "src/**/*.test.{js,jsx}", "src/main.jsx", "src/sw.js"],
        },
    },
});
