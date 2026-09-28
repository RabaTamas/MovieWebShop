import { useSyncExternalStore } from "react";

const subscribe = (callback) => {
    window.addEventListener("online", callback);
    window.addEventListener("offline", callback);
    return () => {
        window.removeEventListener("online", callback);
        window.removeEventListener("offline", callback);
    };
};

/** true, ha a böngésző szerint van hálózati kapcsolat (navigator.onLine), változáskor újrarenderel. */
export default function useOnlineStatus() {
    return useSyncExternalStore(subscribe, () => navigator.onLine, () => true);
}
