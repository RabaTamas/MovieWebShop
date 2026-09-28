/**
 * MovieWebShop Service Worker (vite-plugin-pwa, "injectManifest" mód).
 *
 * 1. Előtöltés: az alkalmazás váza (self.__WB_MANIFEST) → offline is elindul
 * 2. Futásidejű cache: TMDB-képek, egyéb képek, Google Fonts
 *    SZÁNDÉKOSAN NINCS szabály az /api/* (felhasználói adatok), a /hubs/* (SignalR) és a
 *    videóstream (HLS, Azure SAS URL-ek) kéréseire — ezeket a böngésző közvetlenül tölti.
 * 3. Frissítés: a PwaUpdater "Update" gombja SKIP_WAITING üzenettel aktiválja az új verziót
 * 4. Push értesítések: megjelenítés és kattintásra a hivatkozott oldal megnyitása
 */
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching';
import { NavigationRoute, registerRoute } from 'workbox-routing';
import { CacheFirst, StaleWhileRevalidate } from 'workbox-strategies';
import { ExpirationPlugin } from 'workbox-expiration';
import { CacheableResponsePlugin } from 'workbox-cacheable-response';

const DAY = 60 * 60 * 24;

// ── 1. Előtöltött alkalmazásváz ──────────────────────────────────────────────
precacheAndRoute(self.__WB_MANIFEST);
cleanupOutdatedCaches();

// SPA-útvonalak offline is az index.html-re esnek vissza — a szerveroldali útvonalak kivételével
registerRoute(
    new NavigationRoute(createHandlerBoundToURL('/index.html'), {
        denylist: [/^\/api\//, /^\/hubs\//, /^\/swagger/, /^\/hangfire/, /^\/health$/],
    })
);

// ── 2. Futásidejű gyorsítótár ────────────────────────────────────────────────
// TMDB poszterek, háttérképek, szereplőfotók — a tartalmuk az URL-hez kötött, nem változik
registerRoute(
    ({ url }) => url.origin === 'https://image.tmdb.org',
    new CacheFirst({
        cacheName: 'tmdb-images',
        plugins: [
            new CacheableResponsePlugin({ statuses: [0, 200] }),
            new ExpirationPlugin({ maxEntries: 250, maxAgeSeconds: 30 * DAY, purgeOnQuotaError: true }),
        ],
    })
);

// Egyéb képek (pl. admin által megadott külső poszter-URL), aláírt blob URL-ek nélkül
registerRoute(
    ({ request, url }) =>
        request.destination === 'image' &&
        url.origin !== 'https://image.tmdb.org' &&
        !url.pathname.startsWith('/api/') &&
        !url.hostname.endsWith('.blob.core.windows.net') &&
        !url.searchParams.has('sig'),
    new StaleWhileRevalidate({
        cacheName: 'movie-images',
        plugins: [
            new CacheableResponsePlugin({ statuses: [0, 200] }),
            new ExpirationPlugin({ maxEntries: 120, maxAgeSeconds: 7 * DAY, purgeOnQuotaError: true }),
        ],
    })
);

registerRoute(
    ({ url }) => url.origin === 'https://fonts.googleapis.com',
    new StaleWhileRevalidate({ cacheName: 'google-fonts-stylesheets' })
);

registerRoute(
    ({ url }) => url.origin === 'https://fonts.gstatic.com',
    new CacheFirst({
        cacheName: 'google-fonts-webfonts',
        plugins: [
            new CacheableResponsePlugin({ statuses: [0, 200] }),
            new ExpirationPlugin({ maxEntries: 30, maxAgeSeconds: 365 * DAY }),
        ],
    })
);

// ── 3. Frissítés a felhasználó kérésére ──────────────────────────────────────
self.addEventListener('message', (event) => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

// ── 4. Push értesítések ──────────────────────────────────────────────────────
// A backend (PushNotificationService) JSON-t küld: { title, body, icon, image, url, tag }
self.addEventListener('push', (event) => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data?.text() ?? '' };
    }

    const title = data.title || 'MovieWebShop';
    const options = {
        body: data.body || '',
        icon: data.icon || '/pwa-192x192.png',
        badge: '/badge-96x96.png',
        image: data.image || undefined,
        tag: data.tag || undefined,
        // azonos tag esetén az új értesítés is jelezzen (hang/rezgés), ne csak csendben cserélje
        renotify: Boolean(data.tag),
        vibrate: [80, 40, 80],
        data: { url: data.url || '/' },
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

// Kattintásra: ha az alkalmazás már nyitva van, oda navigál és előtérbe hozza; különben új ablakot nyit
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const targetUrl = new URL(event.notification.data?.url || '/', self.location.origin).href;

    event.waitUntil(
        (async () => {
            const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
            const appWindow = windows.find((client) => new URL(client.url).origin === self.location.origin);

            if (appWindow) {
                await appWindow.focus();
                if ('navigate' in appWindow) {
                    try {
                        await appWindow.navigate(targetUrl);
                        return;
                    } catch {
                        /* nem vezérelt ablak — alább új ablakot nyitunk */
                    }
                } else {
                    return;
                }
            }

            await self.clients.openWindow(targetUrl);
        })()
    );
});
