import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminUsers from "./AdminUsers";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const users = [
    { id: 1, name: "neo", email: "neo@zion.io", role: "User" },
    { id: 2, name: "morpheus", email: "m@zion.io", role: "Admin" },
];

const row = (name) => screen.getByText(name, { selector: "td" }).closest("tr");

describe("AdminUsers oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("jogosultságot ad és vesz el, felhasználót töröl", async () => {
        const api = mockApi({
            "GET /api/user/all": users,
            "POST /api/auth/make-admin/1": {},
            "POST /api/auth/remove-admin/2": {},
            "DELETE /api/user/1": {},
        });
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminUsers />);

        expect(await screen.findByText("2 registered users")).toBeInTheDocument();

        confirm.mockReturnValueOnce(false);
        await user.click(within(row("neo")).getByRole("button", { name: /Make Admin/ }));
        expect(api.find("POST", "/api/auth/make-admin/1")).toHaveLength(0);

        await user.click(within(row("neo")).getByRole("button", { name: /Make Admin/ }));
        expect(await within(row("neo")).findByRole("button", { name: /Remove Admin Rights/ })).toBeInTheDocument();

        await user.click(within(row("morpheus")).getByRole("button", { name: /Remove Admin Rights/ }));
        expect(await within(row("morpheus")).findByRole("button", { name: /Make Admin/ })).toBeInTheDocument();

        confirm.mockReturnValueOnce(false);
        await user.click(within(row("neo")).getByRole("button", { name: /Delete/ }));
        expect(api.find("DELETE", "/api/user/1")).toHaveLength(0);
        await user.click(within(row("neo")).getByRole("button", { name: /Delete/ }));
        await waitFor(() => expect(screen.getByText("1 registered users")).toBeInTheDocument());
    });

    it("műveleti hibák a hibaoldalon", async () => {
        const routes = { "GET /api/user/all": users, "POST /api/auth/make-admin/1": { status: 403 } };
        mockApi(routes);
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        const first = renderWithProviders(<AdminUsers />);
        await user.click(within(await screen.findByRole("row", { name: /neo/ })).getByRole("button", { name: /Make Admin/ }));
        expect(await screen.findByText("Error: Error 403: 403")).toBeInTheDocument();
        first.unmount();

        routes["DELETE /api/user/1"] = { status: 500 };
        renderWithProviders(<AdminUsers />);
        await user.click(within(await screen.findByRole("row", { name: /neo/ })).getByRole("button", { name: /Delete/ }));
        expect(await screen.findByText("Error: Error 500: 500")).toBeInTheDocument();
    });

    it("betöltési hiba és hiányzó token", async () => {
        mockApi({ "GET /api/user/all": { status: 500 } });
        const first = renderWithProviders(<AdminUsers />);
        expect(await screen.findByText("Error: Error 500: 500")).toBeInTheDocument();
        first.unmount();

        localStorage.clear();
        renderWithProviders(<AdminUsers />);
        expect(await screen.findByText("Error: Authentication token is missing")).toBeInTheDocument();
    });
});
