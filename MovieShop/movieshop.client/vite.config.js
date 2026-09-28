import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { VitePWA } from 'vite-plugin-pwa';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;

const certificateName = "movieshop.client";
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

// ── Progressive Web App ──────────────────────────────────────────────────────
// Telepíthető alkalmazás (manifest) + saját Service Worker (src/sw.js, "injectManifest" mód):
// előre letölti az alkalmazás vázát, gyorsítótárazza a képeket, és fogadja a push értesítéseket.
// A regisztráció és a frissítésjelzés a src/components/pwa/PwaUpdater.jsx-ben történik
// ("prompt" mód: a felhasználó dönt az újratöltésről).
const pwa = VitePWA({
    strategies: 'injectManifest',
    srcDir: 'src',
    filename: 'sw.js',
    registerType: 'prompt',
    injectRegister: false,
    manifest: {
        id: '/',
        name: 'MovieWebShop',
        short_name: 'MovieShop',
        description: 'Buy, stream and bid on movies — and watch together with friends.',
        lang: 'en',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        orientation: 'any',
        background_color: '#0f0f14',
        theme_color: '#0f0f14',
        categories: ['entertainment', 'shopping'],
        icons: [
            { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
            { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
            { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
    },
    // A precache-lista a buildben keletkező fájlokból készül és a src/sw.js-be injektálódik
    // (self.__WB_MANIFEST). A futásidejű cache-szabályok és a push-kezelés a src/sw.js-ben vannak.
    injectManifest: {
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff2}'],
        maximumFileSizeToCacheInBytes: 4 * 1024 * 1024,
    },
    // Fejlesztői szerveren (npm run dev) nincs Service Worker, hogy ne cache-eljen munka közben
    devOptions: { enabled: false },
});

// https://vitejs.dev/config/
export default defineConfig(({ command }) => {
    // Only generate certificates in development mode
    const isDev = command === 'serve';

    if (isDev && (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath))) {
        if (0 !== child_process.spawnSync('dotnet', [
            'dev-certs',
            'https',
            '--export-path',
            certFilePath,
            '--format',
            'Pem',
            '--no-password',
        ], { stdio: 'inherit', }).status) {
            throw new Error("Could not create certificate.");
        }
    }

    const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
        env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7289';

    // Base config
    const config = {
        // A Tailwind CSS v4 Vite-plugin: a CSS-t build időben generálja,
        // csak a ténylegesen használt osztályokkal
        plugins: [plugin(), tailwindcss(), pwa],
        resolve: {
            alias: {
                '@': fileURLToPath(new URL('./src', import.meta.url))
            }
        }
    };

    // Only add dev server config in development mode
    if (isDev) {
        config.server = {
            proxy: {
                '^/api': {
                    target,
                    secure: false
                }
            },
            port: 5173,
            https: {
                key: fs.readFileSync(keyFilePath),
                cert: fs.readFileSync(certFilePath),
            }
        };
    }

    return config;
});
