import { useEffect } from "react";
import { useRegisterSW } from "virtual:pwa-register/react";
import { toast } from "sonner";

const UPDATE_CHECK_INTERVAL = 60 * 60 * 1000; // óránként

/**
 * Service Worker regisztráció és frissítésjelzés.
 * - Új verzió telepítése után toast jelenik meg; a felhasználó dönt, mikor töltse újra
 *   (addig a régi verzió fut tovább, így nem szakad meg pl. egy fizetés vagy lejátszás).
 * - Az első telepítés után jelzi, hogy az alkalmazás offline is elindul.
 * - Nyitva hagyott alkalmazásnál óránként rákérdez a szerverre új verzióért.
 */
export default function PwaUpdater() {
    const {
        needRefresh: [needRefresh, setNeedRefresh],
        offlineReady: [offlineReady, setOfflineReady],
        updateServiceWorker,
    } = useRegisterSW({
        onRegisteredSW(_swUrl, registration) {
            if (!registration) return;
            setInterval(() => {
                if (navigator.onLine) registration.update();
            }, UPDATE_CHECK_INTERVAL);
        },
        onRegisterError(error) {
            console.error("Service worker registration failed:", error);
        },
    });

    useEffect(() => {
        if (!offlineReady) return;
        toast.success("MovieWebShop is ready to work offline", {
            description: "You can install it from your browser menu (Add to Home screen).",
        });
        setOfflineReady(false);
    }, [offlineReady, setOfflineReady]);

    useEffect(() => {
        if (!needRefresh) return;
        toast("A new version is available", {
            id: "pwa-update",
            description: "Reload to get the latest version of MovieWebShop.",
            duration: Infinity,
            action: { label: "Update", onClick: () => updateServiceWorker(true) },
            cancel: { label: "Later", onClick: () => setNeedRefresh(false) },
        });
    }, [needRefresh, setNeedRefresh, updateServiceWorker]);

    return null;
}
