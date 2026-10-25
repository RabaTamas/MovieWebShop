import { act, render, screen } from "@testing-library/react";

import OfflineBanner from "./OfflineBanner";
import OfflineState from "./OfflineState";
import PwaUpdater from "./PwaUpdater";
import { pwa, toast } from "@/test/fakes";

describe("PWA-komponensek", () => {
    it("OfflineBanner: csak kapcsolat nélkül jelenik meg, és követi a változást", async () => {
        const online = vi.spyOn(navigator, "onLine", "get").mockReturnValue(true);
        render(<OfflineBanner />);
        expect(screen.queryByRole("status")).not.toBeInTheDocument();

        online.mockReturnValue(false);
        act(() => window.dispatchEvent(new Event("offline")));
        expect(screen.getByText("You are offline.")).toBeInTheDocument();
    });

    it("OfflineState: az újrapróbálás újratölti az oldalt", () => {
        const reload = vi.fn();
        vi.stubGlobal("location", { ...window.location, reload });
        render(<OfflineState title="No connection" />);
        screen.getByRole("button", { name: /Try again/ }).click();
        expect(reload).toHaveBeenCalled();
        expect(screen.getByText("No connection")).toBeInTheDocument();
    });

    it("PwaUpdater: offline-készség és új verzió jelzése", () => {
        pwa.offlineReady = true;
        pwa.needRefresh = true;
        render(<PwaUpdater />);

        expect(toast.success).toHaveBeenCalledWith("MovieWebShop is ready to work offline", expect.anything());
        expect(toast).toHaveBeenCalledWith("A new version is available", expect.anything());

        const { action, cancel } = toast.mock.calls[0][1];
        action.onClick();
        expect(pwa.updateServiceWorker).toHaveBeenCalledWith(true);
        act(() => cancel.onClick());
    });

    it("PwaUpdater: óránként frissítést keres, ha van regisztráció és hálózat", () => {
        vi.useFakeTimers();
        try {
            render(<PwaUpdater />);
            const registration = { update: vi.fn() };
            pwa.options.onRegisteredSW("/sw.js", undefined);
            pwa.options.onRegisteredSW("/sw.js", registration);
            pwa.options.onRegisterError(new Error("nope"));

            const online = vi.spyOn(navigator, "onLine", "get").mockReturnValue(false);
            vi.advanceTimersByTime(60 * 60 * 1000);
            expect(registration.update).not.toHaveBeenCalled();
            online.mockReturnValue(true);
            vi.advanceTimersByTime(60 * 60 * 1000);
            expect(registration.update).toHaveBeenCalledTimes(1);
        } finally {
            vi.useRealTimers();
        }
    });
});
