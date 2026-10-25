import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminAddresses from "./AdminAddresses";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const addresses = [
    { id: 1, userName: "neo", userEmail: "neo@zion.io", street: "Fő utca 1", city: "Budapest", zip: "1111", billingOrdersCount: 0 },
    { id: 2, userName: "trinity", userEmail: "t@zion.io", street: "Kossuth tér 2", city: "Szeged", zip: "6720", billingOrdersCount: 3 },
];

const row = (text) => screen.getByText(text, { selector: "td" }).closest("tr");

describe("AdminAddresses oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("címet szerkeszt validációval, töröl, a rendelésben használt nem törölhető", async () => {
        const routes = {
            "GET /api/admin/addresses": addresses,
            "PUT /api/admin/addresses/1": { street: "Váci út 5", city: "Budapest", zip: "1133" },
            "DELETE /api/admin/addresses/1": {},
        };
        const api = mockApi(routes);
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminAddresses />);

        expect(await screen.findByText("Total: 2 addresses")).toBeInTheDocument();
        expect(within(row("Szeged")).getByRole("button", { name: "Delete address" })).toBeDisabled();
        expect(screen.getByText("Used in orders")).toBeInTheDocument();

        await user.click(within(row("Budapest")).getByRole("button", { name: "Edit address" }));
        const street = screen.getByDisplayValue("Fő utca 1");
        const zip = screen.getByDisplayValue("1111");

        await user.clear(street);
        await user.click(screen.getByRole("button", { name: "Save changes" }));
        expect(toast.error).toHaveBeenCalledWith("All fields must be filled!");
        await user.type(street, "Váci út 5");
        await user.clear(zip);
        await user.type(zip, "11a");
        await user.click(screen.getByRole("button", { name: "Save changes" }));
        expect(toast.error).toHaveBeenCalledWith("Zip must be 4 numbers");
        await user.clear(zip);
        await user.type(zip, "1133");
        await user.click(screen.getByRole("button", { name: "Save changes" }));
        expect(await screen.findByText("Váci út 5", { selector: "td" })).toBeInTheDocument();
        expect(api.find("PUT", "/api/admin/addresses/1")[0].body).toEqual({ street: "Váci út 5", city: "Budapest", zip: "1133" });

        await user.click(within(row("Váci út 5")).getByRole("button", { name: "Edit address" }));
        await user.click(screen.getByRole("button", { name: "Cancel" }));
        expect(screen.queryByRole("button", { name: "Save changes" })).not.toBeInTheDocument();

        confirm.mockReturnValueOnce(false);
        await user.click(within(row("Váci út 5")).getByRole("button", { name: "Delete address" }));
        expect(api.find("DELETE", "/api/admin/addresses/1")).toHaveLength(0);
        await user.click(within(row("Váci út 5")).getByRole("button", { name: "Delete address" }));
        await waitFor(() => expect(screen.getByText("Total: 1 addresses")).toBeInTheDocument());
    });

    it("törlési hiba szerverüzenettel vagy állapotkóddal; mentési hiba", async () => {
        const routes = {
            "GET /api/admin/addresses": addresses,
            "DELETE /api/admin/addresses/1": { status: 409, body: "Address in use" },
            "PUT /api/admin/addresses/1": { status: 500 },
        };
        mockApi(routes);
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminAddresses />);

        await screen.findByText("Budapest", { selector: "td" });
        const del = within(row("Budapest")).getByRole("button", { name: "Delete address" });
        await user.click(del);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to delete address: Address in use"));
        routes["DELETE /api/admin/addresses/1"] = { status: 500, body: "" };
        await user.click(del);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to delete address: Error 500: 500"));

        await user.click(within(row("Budapest")).getByRole("button", { name: "Edit address" }));
        await user.click(screen.getByRole("button", { name: "Save changes" }));
        expect(await screen.findByText("Error 500: 500")).toBeInTheDocument();
    });

    it("betöltési hiba, hiányzó token, üres lista", async () => {
        const routes = { "GET /api/admin/addresses": { status: 403 } };
        mockApi(routes);
        const first = renderWithProviders(<AdminAddresses />);
        expect(await screen.findByText("Error 403: 403")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/admin/addresses"] = [];
        const second = renderWithProviders(<AdminAddresses />);
        expect(await screen.findByText("No addresses found in the system.")).toBeInTheDocument();
        second.unmount();

        localStorage.clear();
        renderWithProviders(<AdminAddresses />);
        expect(await screen.findByText("Authentication token is missing")).toBeInTheDocument();
    });
});
