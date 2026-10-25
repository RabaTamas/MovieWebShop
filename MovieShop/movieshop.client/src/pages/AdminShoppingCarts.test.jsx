import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminShoppingCarts from "./AdminShoppingCarts";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const users = [
    { id: 1, name: "neo", email: "neo@zion.io" },
    { id: 2, name: "trinity", email: "trinity@zion.io" },
];
const cart = { id: 5, userId: 1, items: [{ movieId: 7, title: "Matrix", priceAtOrder: 2000, quantity: 2 }] };

describe("AdminShoppingCarts oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("nem admin felhasználót elutasít", async () => {
        loginAs({ role: "User" });
        mockApi({});
        renderWithProviders(<AdminShoppingCarts />);
        expect(await screen.findByText(/Unauthorized access/)).toBeInTheDocument();
    });

    it("listázza a kosarat, mennyiséget módosít, tételt töröl, kosarat ürít", async () => {
        const routes = {
            "GET /api/user/all": users,
            "GET /api/admin/shopping-carts/1": cart,
            "GET /api/admin/shopping-carts/2": { status: 404 },
            "PUT /api/admin/shopping-carts/1/update": {},
            "DELETE /api/admin/shopping-carts/1/remove/7": {},
            "DELETE /api/admin/shopping-carts/1/clear": {},
        };
        const api = mockApi(routes);
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminShoppingCarts />);

        expect(await screen.findByText("Cart for neo")).toBeInTheDocument();
        expect(screen.getAllByText(/4\s?000 Ft/).length).toBeGreaterThan(0);

        await user.click(screen.getByRole("button", { name: "Increase quantity" }));
        await user.click(await screen.findByRole("button", { name: "Decrease quantity" }));
        fireEvent.change(await screen.findByLabelText("Quantity"), { target: { value: "5" } });
        fireEvent.change(screen.getByLabelText("Quantity"), { target: { value: "0" } });
        await waitFor(() => expect(api.find("PUT", "/api/admin/shopping-carts/1/update")).toHaveLength(3));
        expect(api.find("PUT", "/api/admin/shopping-carts/1/update").map((c) => c.body.quantity)).toEqual([3, 1, 5]);

        await user.click(await screen.findByRole("button", { name: /Remove/ }));
        await waitFor(() => expect(api.find("DELETE", "/api/admin/shopping-carts/1/remove/7")).toHaveLength(1));

        await user.click(await screen.findByRole("button", { name: /Clear Cart/ }));
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Cart cleared successfully!"));

        // Megerősítés nélkül nem töröl
        confirm.mockReturnValue(false);
        await user.click(await screen.findByRole("button", { name: /Remove/ }));
        await user.click(screen.getByRole("button", { name: /Clear Cart/ }));
        expect(api.find("DELETE", "/api/admin/shopping-carts/1/clear")).toHaveLength(1);

        // Másik felhasználó: nincs kosara (404) → üres
        await user.selectOptions(screen.getByLabelText("Select User"), "2");
        expect(await screen.findByText("This user's cart is empty.")).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Clear Cart/ })).toBeDisabled();
    });

    it("műveleti hibák toastként", async () => {
        mockApi({
            "GET /api/user/all": users,
            "GET /api/admin/shopping-carts/1": cart,
            "PUT /api/admin/shopping-carts/1/update": { status: 500 },
            "DELETE /api/admin/shopping-carts/1/remove/7": { status: 500 },
            "DELETE /api/admin/shopping-carts/1/clear": { status: 500 },
        });
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminShoppingCarts />);

        await user.click(await screen.findByRole("button", { name: "Increase quantity" }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/^Failed to update quantity: Error 500/)));
        await user.click(screen.getByRole("button", { name: /Remove/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/^Failed to remove item/)));
        await user.click(screen.getByRole("button", { name: /Clear Cart/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/^Failed to clear cart/)));
    });

    it("betöltési hibák: felhasználók, kosár; üres felhasználólista", async () => {
        const routes = { "GET /api/user/all": { status: 500 } };
        mockApi(routes);
        const { unmount } = renderWithProviders(<AdminShoppingCarts />);
        expect(await screen.findByText(/Error: Error 500/)).toBeInTheDocument();
        unmount();

        routes["GET /api/user/all"] = users;
        routes["GET /api/admin/shopping-carts/1"] = { status: 500 };
        const second = renderWithProviders(<AdminShoppingCarts />);
        expect(await screen.findByText(/Error: Error 500/)).toBeInTheDocument();
        second.unmount();

        routes["GET /api/user/all"] = [];
        renderWithProviders(<AdminShoppingCarts />);
        expect(await screen.findByText("Manage Shopping Carts")).toBeInTheDocument();
        expect(screen.queryByText(/Cart for/)).not.toBeInTheDocument();
    });
});
