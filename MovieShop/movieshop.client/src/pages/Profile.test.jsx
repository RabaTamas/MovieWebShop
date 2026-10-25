import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import Profile from "./Profile";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

describe("Profile oldal", () => {
    beforeEach(() => loginAs({ name: "neo" }));

    it("e-mailt és jelszót módosít", async () => {
        const routes = {
            "GET /api/user/profile": { name: "Neo", email: "neo@zion.io" },
            "PUT /api/user/email": {},
            "PUT /api/user/password": {},
        };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<Profile />);

        expect(await screen.findByText("Current: neo@zion.io")).toBeInTheDocument();
        expect(screen.getByText("N")).toBeInTheDocument();
        expect(screen.getByText("Your browser doesn't support push notifications.")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /Manage Two-Factor Authentication/ })).toHaveAttribute("href", "/profile/2fa");

        await user.type(screen.getByLabelText("New email"), "the.one@zion.io");
        await user.click(screen.getByRole("button", { name: "Update" }));
        expect(await screen.findByText("Email updated!")).toBeInTheDocument();
        expect(screen.getByText("Current: the.one@zion.io")).toBeInTheDocument();
        expect(api.find("PUT", "/api/user/email")[0].body).toEqual({ newEmail: "the.one@zion.io" });

        await user.type(screen.getByLabelText("Current password"), "OldPass1!");
        await user.type(screen.getByLabelText("New password"), "NewPass1!");
        await user.click(screen.getByRole("button", { name: "Change" }));
        expect(await screen.findByText("Password successfully changed")).toBeInTheDocument();
        expect(api.find("PUT", "/api/user/password")[0].body).toEqual({ currentPassword: "OldPass1!", newPassword: "NewPass1!" });
    });

    it("módosítási hibák: szerverüzenet, alapértelmezett szöveg, hálózati hiba", async () => {
        const routes = {
            "GET /api/user/profile": { userName: "neo_a", email: "neo@zion.io" },
            "PUT /api/user/email": { status: 400, body: "Email taken" },
            "PUT /api/user/password": { status: 400, body: "" },
        };
        mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<Profile />);
        await screen.findByText("Current: neo@zion.io");

        await user.type(screen.getByLabelText("New email"), "x@y.io");
        await user.click(screen.getByRole("button", { name: "Update" }));
        expect(await screen.findByText("Email taken")).toBeInTheDocument();
        routes["PUT /api/user/email"] = { status: 400, body: "" };
        await user.click(screen.getByRole("button", { name: "Update" }));
        expect(await screen.findByText("Error happened")).toBeInTheDocument();
        routes["PUT /api/user/email"] = new Error("down");
        await user.click(screen.getByRole("button", { name: "Update" }));
        expect(await screen.findByText("Unsuccessful connection to the server.")).toBeInTheDocument();

        await user.type(screen.getByLabelText("Current password"), "OldPass1!");
        await user.type(screen.getByLabelText("New password"), "NewPass1!");
        await user.click(screen.getByRole("button", { name: "Change" }));
        expect(await screen.findByText("Error happened")).toBeInTheDocument();
        routes["PUT /api/user/password"] = new Error("down");
        await user.click(screen.getByRole("button", { name: "Change" }));
        expect(await screen.findAllByText("Unsuccessful connection to the server.")).toHaveLength(2);
    });

    it("lejárt munkamenetnél kijelentkeztet; egyéb hibánál üzenet; token nélkül", async () => {
        const routes = { "GET /api/user/profile": { status: 401 } };
        mockApi(routes);
        const first = renderWithProviders(<Profile />);
        expect(await screen.findByText("Authentication failed. Please log in again.")).toBeInTheDocument();
        expect(localStorage.getItem("jwt")).toBeNull();
        first.unmount();

        loginAs();
        routes["GET /api/user/profile"] = { status: 500 };
        const second = renderWithProviders(<Profile />);
        expect(await screen.findByText("Failed to get the profile")).toBeInTheDocument();
        second.unmount();

        localStorage.clear();
        renderWithProviders(<Profile />);
        expect(await screen.findByText("No authentication token found")).toBeInTheDocument();
    });
});
