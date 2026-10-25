import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import TwoFactorSetup from "./TwoFactorSetup";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const renderPage = () =>
    renderWithProviders(<TwoFactorSetup />, {
        route: "/2fa",
        path: "/2fa",
        routes: <Route path="/profile" element={<p>profile page</p>} />,
    });

describe("TwoFactorSetup oldal", () => {
    beforeEach(() => loginAs());

    it("bekapcsolja a 2FA-t: QR-kód, kódellenőrzés, helyreállító kódok", async () => {
        const routes = {
            "GET /api/Auth/2fa/status": { isEnabled: false },
            "GET /api/Auth/2fa/setup": { sharedKey: "ABCD EFGH", authenticatorUri: "otpauth://totp/MovieShop:neo?secret=ABCD" },
            "POST /api/Auth/2fa/enable": { status: 400, body: { message: "Invalid code" } },
        };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("disabled")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Set up 2FA/ }));
        expect(await screen.findByText("ABCD EFGH")).toBeInTheDocument();
        expect(document.querySelector("svg")).toBeInTheDocument();

        const input = screen.getByPlaceholderText("000000");
        await user.type(input, "12a3456");
        expect(input).toHaveValue("123456");
        await user.click(screen.getByRole("button", { name: "Enable 2FA" }));
        expect(await screen.findByText("Invalid code")).toBeInTheDocument();
        expect(input).toHaveValue("");

        routes["POST /api/Auth/2fa/enable"] = { recoveryCodes: ["r-1", "r-2"] };
        await user.type(input, "654321");
        await user.click(screen.getByRole("button", { name: "Enable 2FA" }));
        expect(await screen.findByText("2FA Enabled Successfully!")).toBeInTheDocument();
        expect(screen.getByText("r-2")).toBeInTheDocument();
        expect(api.find("POST", "/api/Auth/2fa/enable")[1].body).toEqual({ code: "654321" });

        await user.click(screen.getByRole("button", { name: "Done" }));
        expect(screen.getByText("profile page")).toBeInTheDocument();
    });

    it("a beállítás megszakítható; a beállítás indítása hibát jelezhet", async () => {
        const routes = {
            "GET /api/Auth/2fa/status": { isEnabled: false },
            "GET /api/Auth/2fa/setup": { status: 500, body: { message: "Setup unavailable" } },
        };
        mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        await user.click(await screen.findByRole("button", { name: /Set up 2FA/ }));
        expect(await screen.findByText("Setup unavailable")).toBeInTheDocument();

        routes["GET /api/Auth/2fa/setup"] = { sharedKey: "K", authenticatorUri: "otpauth://x" };
        await user.click(screen.getByRole("button", { name: /Set up 2FA/ }));
        await user.click(await screen.findByRole("button", { name: "Cancel" }));
        expect(screen.getByRole("button", { name: /Set up 2FA/ })).toBeInTheDocument();
    });

    it("kikapcsolja a 2FA-t; hibás kódnál jelez; megszakítható", async () => {
        const routes = {
            "GET /api/Auth/2fa/status": { isEnabled: true },
            "POST /api/Auth/2fa/disable": { status: 400, body: { message: "Wrong code" } },
        };
        mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("enabled")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Disable 2FA/ }));
        await user.click(screen.getByRole("button", { name: "Cancel" }));
        await user.click(screen.getByRole("button", { name: /Disable 2FA/ }));

        await user.type(screen.getByPlaceholderText("000000"), "111111");
        await user.click(screen.getByRole("button", { name: "Disable 2FA" }));
        expect(await screen.findByText("Wrong code")).toBeInTheDocument();

        routes["POST /api/Auth/2fa/disable"] = {};
        await user.type(screen.getByPlaceholderText("000000"), "222222");
        await user.click(screen.getByRole("button", { name: "Disable 2FA" }));
        expect(await screen.findByText("disabled")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Back/ }));
        expect(screen.getByText("profile page")).toBeInTheDocument();
    });

    it("az állapot betöltési hibáját jelzi", async () => {
        mockApi({ "GET /api/Auth/2fa/status": new Error("down") });
        renderPage();
        expect(await screen.findByText("Failed to load 2FA status")).toBeInTheDocument();
    });
});
