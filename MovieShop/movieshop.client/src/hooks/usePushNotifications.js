import { useCallback, useEffect, useState } from "react";

import API_BASE_URL from "../config/api";
import { useAuth } from "../contexts/AuthContext";

const supported =
    typeof window !== "undefined" &&
    "serviceWorker" in navigator &&
    "PushManager" in window &&
    "Notification" in window;

const SUBSCRIBE_TIMEOUT_MS = 20000;

const isIos = typeof navigator !== "undefined" && /iphone|ipad|ipod/i.test(navigator.userAgent);
const isStandalone =
    typeof window !== "undefined" &&
    (window.matchMedia?.("(display-mode: standalone)").matches || navigator.standalone === true);

/** A base64url VAPID kulcs átalakítása a PushManager által várt Uint8Array-re. */
function urlBase64ToUint8Array(base64String) {
    const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
    const raw = atob(base64);
    return Uint8Array.from([...raw].map((char) => char.charCodeAt(0)));
}

function sameKey(buffer, keyBytes) {
    if (!buffer) return false;
    const current = new Uint8Array(buffer);
    return current.length === keyBytes.length && current.every((byte, i) => byte === keyBytes[i]);
}

/** Aktív Service Worker megvárása — fejlesztői szerveren nincs, ezért időkorláttal. */
function waitForServiceWorker(timeoutMs = 5000) {
    return Promise.race([
        navigator.serviceWorker.ready,
        new Promise((resolve) => setTimeout(() => resolve(null), timeoutMs)),
    ]);
}

/**
 * Web Push feliratkozás kezelése a böngészőben.
 * - supported / swReady: tudja-e a böngésző, és fut-e a Service Worker (csak buildelt verzióban)
 * - permission: "default" | "granted" | "denied"
 * - subscribe(): engedélykérés → PushManager.subscribe (VAPID kulccsal) → mentés a backenden
 * - unsubscribe(), sendTest()
 * iOS-en a push csak a kezdőképernyőre telepített alkalmazásban érhető el (iOS 16.4+).
 */
export default function usePushNotifications() {
    const { token } = useAuth();
    const [swReady, setSwReady] = useState(false);
    const [permission, setPermission] = useState(supported ? Notification.permission : "unsupported");
    const [subscription, setSubscription] = useState(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (!supported) return;
        let cancelled = false;

        waitForServiceWorker().then(async (registration) => {
            if (cancelled || !registration) return;
            setSwReady(true);
            const existing = await registration.pushManager.getSubscription();
            if (!cancelled) setSubscription(existing);
        });

        return () => {
            cancelled = true;
        };
    }, []);

    const subscribe = useCallback(async () => {
        if (!supported) return { ok: false, error: "Your browser doesn't support push notifications." };
        setBusy(true);
        try {
            const result = await Notification.requestPermission();
            setPermission(result);
            if (result !== "granted") {
                throw new Error(result === "denied"
                    ? "Notifications are blocked. Allow them in your browser's site settings."
                    : "Notification permission was not granted.");
            }

            const keyResponse = await fetch(`${API_BASE_URL}/api/Push/vapid-public-key`);
            if (!keyResponse.ok) throw new Error("Push notifications are not available on the server.");
            const { publicKey } = await keyResponse.json();
            const applicationServerKey = urlBase64ToUint8Array(publicKey);

            const registration = await waitForServiceWorker();
            if (!registration) throw new Error("The app's service worker is not active yet. Reload the page and try again.");

            let current = await registration.pushManager.getSubscription();
            // Ha a szerver kulcsa megváltozott, a régi feliratkozás használhatatlan
            if (current && !sameKey(current.options?.applicationServerKey, applicationServerKey)) {
                await current.unsubscribe();
                current = null;
            }
            if (!current) {
                // Elérhetetlen push-szolgáltatónál (pl. tiltott FCM, headless Chrome) a subscribe() sosem tér vissza
                current = await Promise.race([
                    registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey }),
                    new Promise((_, reject) => setTimeout(
                        () => reject(new Error("The browser's push service didn't respond. Check your connection and try again.")),
                        SUBSCRIBE_TIMEOUT_MS)),
                ]);
            }

            const saveResponse = await fetch(`${API_BASE_URL}/api/Push/subscribe`, {
                method: "POST",
                headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
                body: JSON.stringify(current.toJSON()),
            });
            if (!saveResponse.ok) throw new Error("Could not save the subscription on the server.");

            setSubscription(current);
            return { ok: true };
        } catch (error) {
            return { ok: false, error: error.message };
        } finally {
            setBusy(false);
        }
    }, [token]);

    const unsubscribe = useCallback(async () => {
        if (!subscription) return { ok: true };
        setBusy(true);
        try {
            if (token) {
                await fetch(`${API_BASE_URL}/api/Push/unsubscribe`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
                    body: JSON.stringify({ endpoint: subscription.endpoint }),
                }).catch(() => {});
            }
            await subscription.unsubscribe();
            setSubscription(null);
            return { ok: true };
        } catch (error) {
            return { ok: false, error: error.message };
        } finally {
            setBusy(false);
        }
    }, [subscription, token]);

    const sendTest = useCallback(async () => {
        setBusy(true);
        try {
            const response = await fetch(`${API_BASE_URL}/api/Push/test`, {
                method: "POST",
                headers: { Authorization: `Bearer ${token}` },
            });
            if (!response.ok) throw new Error("The test notification could not be sent.");
            const result = await response.json();
            return { ok: result.sent > 0, result };
        } catch (error) {
            return { ok: false, error: error.message };
        } finally {
            setBusy(false);
        }
    }, [token]);

    return {
        supported,
        swReady,
        permission,
        subscription,
        subscribed: Boolean(subscription),
        busy,
        isIos,
        isStandalone,
        needsInstallFirst: isIos && !isStandalone,
        subscribe,
        unsubscribe,
        sendTest,
    };
}
