import { useState } from "react";
import { pwa } from "./fakes";

/** A vite-plugin-pwa `virtual:pwa-register/react` moduljának helyettesítője. */
export function useRegisterSW(options) {
    pwa.options = options;
    const needRefresh = useState(pwa.needRefresh);
    const offlineReady = useState(pwa.offlineReady);
    return { needRefresh, offlineReady, updateServiceWorker: pwa.updateServiceWorker };
}
