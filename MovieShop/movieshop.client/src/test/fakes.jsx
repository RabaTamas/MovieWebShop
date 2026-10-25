/**
 * Böngészőn kívül nem futtatható külső könyvtárak helyettesítői a tesztekhez:
 * SignalR, HLS.js, Stripe, Google-bejelentkezés és a PWA-regisztráció.
 * A setup.js globálisan köti be őket; a tesztek az itt exportált vezérlőkkel
 * szimulálnak szervereseményt, fizetési eredményt stb.
 */
import { vi } from "vitest";

// ── SignalR ──────────────────────────────────────────────────────────────────

export const signalr = {
    connections: [],
    /** Az invoke-ok visszatérési értékei metódusnév szerint (pl. CreateRoom → szobakód). */
    invokeResults: {},
    failStart: false,
    last() {
        return this.connections[this.connections.length - 1];
    },
    /** Szerver által küldött üzenet szimulálása az utolsó kapcsolaton. */
    emit(event, ...args) {
        this.last().handlers[event]?.forEach((h) => h(...args));
    },
    reset() {
        this.connections = [];
        this.invokeResults = {};
        this.failStart = false;
    },
};

class FakeConnection {
    constructor(url, options) {
        this.url = url;
        this.options = options;
        this.handlers = {};
        this.invocations = [];
        this.state = "Disconnected";
        signalr.connections.push(this);
    }
    on(event, handler) {
        (this.handlers[event] ??= []).push(handler);
    }
    off(event) {
        delete this.handlers[event];
    }
    onreconnecting() {}
    onreconnected() {}
    onclose() {}
    async start() {
        if (signalr.failStart) throw new Error("connection failed");
        this.state = "Connected";
    }
    async stop() {
        this.state = "Disconnected";
    }
    async invoke(method, ...args) {
        this.invocations.push([method, ...args]);
        const result = signalr.invokeResults[method];
        if (result instanceof Error) throw result;
        return typeof result === "function" ? result(...args) : result;
    }
}

class FakeHubConnectionBuilder {
    withUrl(url, options) {
        this.url = url;
        this.options = options;
        return this;
    }
    withAutomaticReconnect() {
        return this;
    }
    configureLogging() {
        return this;
    }
    build() {
        return new FakeConnection(this.url, this.options);
    }
}

export const signalrModule = {
    HubConnectionBuilder: FakeHubConnectionBuilder,
    LogLevel: { Warning: 3, Information: 2, Error: 4 },
    HubConnectionState: { Connected: "Connected", Disconnected: "Disconnected" },
};

// ── HLS.js ───────────────────────────────────────────────────────────────────

export const hls = {
    instances: [],
    supported: true,
    last() {
        return this.instances[this.instances.length - 1];
    },
    reset() {
        this.instances = [];
        this.supported = true;
    },
};

class FakeHls {
    static Events = { MANIFEST_PARSED: "hlsManifestParsed", ERROR: "hlsError", LEVEL_SWITCHED: "hlsLevelSwitched" };
    static ErrorTypes = { NETWORK_ERROR: "networkError", MEDIA_ERROR: "mediaError", OTHER_ERROR: "otherError" };
    static isSupported() {
        return hls.supported;
    }
    constructor(config) {
        this.config = config;
        this.handlers = {};
        this.levels = [{ height: 480 }, { height: 720 }, { height: 1080 }];
        this.currentLevel = -1;
        this.loadSource = vi.fn();
        this.attachMedia = vi.fn();
        this.startLoad = vi.fn();
        this.recoverMediaError = vi.fn();
        this.destroy = vi.fn();
        hls.instances.push(this);
    }
    on(event, handler) {
        (this.handlers[event] ??= []).push(handler);
    }
    emit(event, data = {}) {
        this.handlers[event]?.forEach((h) => h(event, data));
    }
}

export const hlsModule = { default: FakeHls };

// ── Stripe ───────────────────────────────────────────────────────────────────

export const stripe = {
    paymentMethodResult: { paymentMethod: { id: "pm_test" } },
    confirmResult: { paymentIntent: { id: "pi_test", status: "succeeded" } },
    reset() {
        this.paymentMethodResult = { paymentMethod: { id: "pm_test" } };
        this.confirmResult = { paymentIntent: { id: "pi_test", status: "succeeded" } };
    },
};

const fakeStripeInstance = {
    createPaymentMethod: vi.fn(async () => stripe.paymentMethodResult),
    confirmCardPayment: vi.fn(async () => stripe.confirmResult),
};

export const stripeJsModule = { loadStripe: vi.fn(async () => fakeStripeInstance) };

export const reactStripeModule = {
    Elements: ({ children }) => <div data-testid="stripe-elements">{children}</div>,
    CardElement: () => <div data-testid="card-element" />,
    useStripe: () => fakeStripeInstance,
    useElements: () => ({ getElement: () => ({}) }),
};

// ── Google-bejelentkezés ─────────────────────────────────────────────────────

export const googleModule = {
    GoogleOAuthProvider: ({ children }) => <>{children}</>,
    GoogleLogin: ({ onSuccess, onError }) => (
        <>
            <button type="button" onClick={() => onSuccess({ credential: "google-id-token" })}>Google sign-in</button>
            <button type="button" onClick={() => onError()}>Google error</button>
        </>
    ),
};

// ── PWA (vite-plugin-pwa virtuális modul) ────────────────────────────────────

export const pwa = {
    needRefresh: false,
    offlineReady: false,
    updateServiceWorker: vi.fn(),
    registration: { update: vi.fn() },
    options: null,
    reset() {
        this.needRefresh = false;
        this.offlineReady = false;
        this.options = null;
    },
};

// ── Sonner (toast) ───────────────────────────────────────────────────────────

/** A toast-hívások ellenőrizhetők: toast.success / toast.error stb. vi.fn(). */
export const toast = Object.assign(vi.fn(), {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
    message: vi.fn(),
    loading: vi.fn(),
    dismiss: vi.fn(),
    promise: vi.fn(),
});

export const sonnerModule = { toast, Toaster: () => null };
