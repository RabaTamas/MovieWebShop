import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import NotificationSettings from "@/components/pwa/NotificationSettings";
import PushPrompt from "@/components/pwa/PushPrompt";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

// A hook betöltéskor dönti el, támogatott-e a push, ezért a böngésző-API-k az importok előtt kellenek
const push = vi.hoisted(() => {
    const state = {
        existing: null,
        subscribeImpl: null,
        registration: null,
        readyNever: false,
    };
    state.makeSubscription = (key = [1, 2, 3]) => ({
        endpoint: "https://push.example/abc",
        options: { applicationServerKey: new Uint8Array(key).buffer },
        toJSON: () => ({ endpoint: "https://push.example/abc", keys: { p256dh: "k", auth: "a" } }),
        unsubscribe: vi.fn(async () => true),
    });
    state.reset = () => {
        state.existing = null;
        state.readyNever = false;
        state.subscribeImpl = async () => state.makeSubscription();
        state.registration = {
            pushManager: {
                getSubscription: async () => state.existing,
                subscribe: (...args) => state.subscribeImpl(...args),
            },
        };
        window.Notification.permission = "default";
        window.Notification.requestPermission = async () => "granted";
    };
    Object.defineProperty(navigator, "serviceWorker", {
        configurable: true,
        value: {
            get ready() {
                return state.readyNever ? new Promise(() => {}) : Promise.resolve(state.registration);
            },
        },
    });
    window.PushManager = function PushManager() {};
    window.Notification = { permission: "default", requestPermission: async () => "granted" };
    return state;
});

// "AQID" base64url = [1, 2, 3]
const vapid = { "GET /api/Push/vapid-public-key": { publicKey: "AQID" } };

describe("usePushNotifications + push-felületek", () => {
    beforeEach(() => {
        push.reset();
        loginAs();
    });

    it("PushPrompt: feliratkozik, majd eltűnik", async () => {
        const api = mockApi({ ...vapid, "POST /api/Push/subscribe": {} });
        const user = userEvent.setup();
        renderWithProviders(<PushPrompt />);

        expect(await screen.findByText("Never miss a premiere")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: "Enable" }));
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Notifications enabled", expect.anything()));
        await waitFor(() => expect(screen.queryByText("Never miss a premiere")).not.toBeInTheDocument());
        expect(api.find("POST", "/api/Push/subscribe")[0].body).toEqual({ endpoint: "https://push.example/abc", keys: { p256dh: "k", auth: "a" } });
    });

    it("PushPrompt: hiba esetén toast; a bezárás megjegyződik", async () => {
        window.Notification.requestPermission = async () => "denied";
        mockApi({});
        const user = userEvent.setup();
        const { unmount } = renderWithProviders(<PushPrompt />);

        await user.click(await screen.findByRole("button", { name: "Enable" }));
        await waitFor(() =>
            expect(toast.error).toHaveBeenCalledWith("Could not enable notifications", {
                description: "Notifications are blocked. Allow them in your browser's site settings.",
            })
        );

        await user.click(screen.getByRole("button", { name: "Not now" }));
        await waitFor(() => expect(screen.queryByText("Never miss a premiere")).not.toBeInTheDocument());
        expect(localStorage.getItem("movieshop-push-prompt-dismissed")).toBe("1");
        unmount();

        renderWithProviders(<PushPrompt />);
        await act(async () => {});
        expect(screen.queryByText("Never miss a premiere")).not.toBeInTheDocument();
    });

    it("NotificationSettings: feliratkozási hibaágak", async () => {
        const routes = { "GET /api/Push/vapid-public-key": { status: 503 } };
        mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<NotificationSettings />);
        const enable = await screen.findByRole("button", { name: /Enable notifications/ });
        const lastError = () => toast.error.mock.calls.at(-1)[1].description;

        window.Notification.requestPermission = async () => "default";
        await user.click(enable);
        await waitFor(() => expect(lastError()).toBe("Notification permission was not granted."));

        window.Notification.requestPermission = async () => "granted";
        await user.click(enable);
        await waitFor(() => expect(lastError()).toBe("Push notifications are not available on the server."));

        Object.assign(routes, vapid, { "POST /api/Push/subscribe": { status: 500 } });
        await user.click(enable);
        await waitFor(() => expect(lastError()).toBe("Could not save the subscription on the server."));
    });

    it("a régi kulcsú feliratkozást lecseréli, az egyezőt újrahasznosítja", async () => {
        const stale = push.makeSubscription([9, 9]);
        push.existing = stale;
        const subscribe = vi.fn(async () => push.makeSubscription());
        push.subscribeImpl = subscribe;
        mockApi({ ...vapid, "POST /api/Push/subscribe": {} });
        const user = userEvent.setup();
        renderWithProviders(<NotificationSettings />);

        // Létező feliratkozással „On" állapotban indul
        expect(await screen.findByText("On")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Turn off/ }));
        await waitFor(() => expect(toast).toHaveBeenCalledWith("Notifications turned off"));
        expect(stale.unsubscribe).toHaveBeenCalled();

        // Újrafeliratkozás: a tárolt (régi kulcsú) feliratkozás lecserélődik
        push.existing = push.makeSubscription([9, 9]);
        await user.click(await screen.findByRole("button", { name: /Enable notifications/ }));
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Notifications enabled", expect.anything()));
        expect(push.existing.unsubscribe).toHaveBeenCalled();
        expect(subscribe).toHaveBeenCalledWith({ userVisibleOnly: true, applicationServerKey: new Uint8Array([1, 2, 3]) });

        // Egyező kulcsnál nem iratkozik fel újra
        await user.click(screen.getByRole("button", { name: /Turn off/ }));
        push.existing = push.makeSubscription([1, 2, 3]);
        subscribe.mockClear();
        await user.click(await screen.findByRole("button", { name: /Enable notifications/ }));
        await waitFor(() => expect(screen.getByText("On")).toBeInTheDocument());
        expect(subscribe).not.toHaveBeenCalled();
    });

    it("próbaértesítés és leiratkozási hiba", async () => {
        const sub = push.makeSubscription();
        push.existing = sub;
        const routes = { "POST /api/Push/test": { sent: 2 }, "POST /api/Push/unsubscribe": {} };
        mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<NotificationSettings />);

        const testButton = await screen.findByRole("button", { name: /Send test notification/ });
        await user.click(testButton);
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Test notification sent", { description: "Delivered to 2 devices." }));
        routes["POST /api/Push/test"] = { sent: 1 };
        await user.click(testButton);
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Test notification sent", { description: "Delivered to 1 device." }));
        routes["POST /api/Push/test"] = { sent: 0 };
        await user.click(testButton);
        await waitFor(() =>
            expect(toast.error).toHaveBeenCalledWith("No notification was delivered", { description: "No active subscription for this account." })
        );
        routes["POST /api/Push/test"] = { status: 500 };
        await user.click(testButton);
        await waitFor(() =>
            expect(toast.error).toHaveBeenCalledWith("No notification was delivered", { description: "The test notification could not be sent." })
        );

        sub.unsubscribe.mockRejectedValueOnce(new Error("browser refused"));
        await user.click(screen.getByRole("button", { name: /Turn off/ }));
        await waitFor(() =>
            expect(toast.error).toHaveBeenCalledWith("Could not turn off notifications", { description: "browser refused" })
        );
    });

    it("leiratkozás bejelentkezés nélkül csak a böngészőben", async () => {
        localStorage.clear();
        const sub = push.makeSubscription();
        push.existing = sub;
        const api = mockApi({});
        const user = userEvent.setup();
        renderWithProviders(<NotificationSettings />);

        await user.click(await screen.findByRole("button", { name: /Turn off/ }));
        await waitFor(() => expect(sub.unsubscribe).toHaveBeenCalled());
        expect(api.find("POST", "/api/Push/unsubscribe")).toHaveLength(0);
    });

    it("tiltott engedélynél tájékoztat", async () => {
        window.Notification.permission = "denied";
        mockApi({});
        renderWithProviders(<NotificationSettings />);
        expect(await screen.findByText(/Notifications are blocked for this site/)).toBeInTheDocument();
    });

    it("Service Worker nélkül (fejlesztői szerver) jelzi, hogy csak a buildelt verzióban érhető el", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        try {
            push.readyNever = true;
            mockApi(vapid);
            const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
            const { unmount } = renderWithProviders(<NotificationSettings />);
            await act(async () => vi.advanceTimersByTimeAsync(5000));
            expect(screen.getByText(/available in the installed \/ production build/)).toBeInTheDocument();
            unmount();

            // Feliratkozáskor sem jön létre a Service Worker
            push.readyNever = false;
            renderWithProviders(<NotificationSettings />);
            const enable = await screen.findByRole("button", { name: /Enable notifications/ });
            push.readyNever = true;
            await user.click(enable);
            await act(async () => vi.advanceTimersByTimeAsync(5000));
            await waitFor(() =>
                expect(toast.error).toHaveBeenCalledWith("Could not enable notifications", {
                    description: "The app's service worker is not active yet. Reload the page and try again.",
                })
            );
        } finally {
            vi.useRealTimers();
        }
    });

    it("nem válaszoló push-szolgáltatónál 20 mp után feladja", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        try {
            push.subscribeImpl = () => new Promise(() => {});
            mockApi(vapid);
            const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
            renderWithProviders(<NotificationSettings />);

            await user.click(await screen.findByRole("button", { name: /Enable notifications/ }));
            await act(async () => vi.advanceTimersByTimeAsync(20_000));
            await waitFor(() =>
                expect(toast.error).toHaveBeenCalledWith("Could not enable notifications", {
                    description: "The browser's push service didn't respond. Check your connection and try again.",
                })
            );
        } finally {
            vi.useRealTimers();
        }
    });

    it("iOS-en telepítést kér az engedélyezés előtt", async () => {
        const ua = vi.spyOn(navigator, "userAgent", "get").mockReturnValue("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0)");
        vi.resetModules();
        const { AuthProvider } = await import("@/contexts/AuthContext");
        const { default: IosSettings } = await import("@/components/pwa/NotificationSettings");
        render(
            <AuthProvider>
                <IosSettings />
            </AuthProvider>
        );
        expect(await screen.findByText(/add MovieWebShop to your Home Screen first/)).toBeInTheDocument();
        ua.mockRestore();
    });
});
