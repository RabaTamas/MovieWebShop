import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import AdminOrderDetails from "./AdminOrderDetails";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const order = (extra = {}) => ({
    id: 12,
    orderDate: "2030-01-01T10:00:00Z",
    totalPrice: 5000,
    userName: "neo",
    userEmail: "neo@zion.io",
    userId: 1,
    status: "Pending",
    billingAddress: { fullName: "Neo Anderson", street: "Fő utca 1", city: "Budapest", state: "Pest", zip: "1111", country: "HU", phone: "+36 1" },
    movies: [
        { movieId: 1, title: "Matrix", priceAtOrder: 2000 },
        { movieId: 2, title: "Alien", priceAtOrder: 1500, quantity: 2 },
    ],
    ...extra,
});

const renderPage = () =>
    renderWithProviders(<AdminOrderDetails />, {
        route: "/admin/orders/12",
        path: "/admin/orders/:id",
        routes: <Route path="/admin/orders" element={<p>order list</p>} />,
    });

describe("AdminOrderDetails oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("megjeleníti a rendelést, állapotot vált és lemond", async () => {
        const routes = { "GET /api/admin/orders/12": order(), "PUT /api/admin/orders/12/status": {} };
        const api = mockApi(routes);
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("Order #12 Details")).toBeInTheDocument();
        expect(screen.getByText("Neo Anderson")).toBeInTheDocument();
        expect(screen.getByText("Budapest, Pest 1111")).toBeInTheDocument();
        expect(screen.getByText("+36 1")).toBeInTheDocument();
        expect(screen.getByText(/^3\s?000 Ft$/)).toBeInTheDocument();

        const update = screen.getByRole("button", { name: "Update" });
        expect(update).toBeDisabled();
        await user.selectOptions(screen.getByLabelText("New status"), "Completed");
        await user.click(update);
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Order status updated successfully!"));
        expect(api.find("PUT", "/api/admin/orders/12/status")[0].body).toEqual({ status: "Completed" });

        confirm.mockReturnValueOnce(false);
        await user.click(screen.getByRole("button", { name: /Cancel Order/ }));
        expect(api.find("PUT", "/api/admin/orders/12/status")).toHaveLength(1);
        await user.click(screen.getByRole("button", { name: /Cancel Order/ }));
        await waitFor(() => expect(screen.queryByRole("button", { name: /Cancel Order/ })).not.toBeInTheDocument());
        expect(api.find("PUT", "/api/admin/orders/12/status")[1].body).toEqual({ status: "Cancelled" });

        routes["PUT /api/admin/orders/12/status"] = { status: 500 };
        await user.selectOptions(screen.getByLabelText("New status"), "Refunded");
        await user.click(screen.getByRole("button", { name: "Update" }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to update order status. Please try again."));

        await user.click(screen.getAllByRole("button", { name: /Back to Orders/ }).at(-1));
        expect(screen.getByText("order list")).toBeInTheDocument();
    });

    it("minimális cím (zipCode mezővel) és cím nélküli rendelés", async () => {
        const routes = { "GET /api/admin/orders/12": order({ billingAddress: { street: "Fő utca 1", city: "Budapest", zipCode: "2222" } }) };
        mockApi(routes);
        const first = renderPage();
        expect(await screen.findByText("Budapest 2222")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/admin/orders/12"] = order({ billingAddress: null });
        renderPage();
        expect(await screen.findByText("Order Items")).toBeInTheDocument();
        expect(screen.queryByText("Billing Address")).not.toBeInTheDocument();
    });

    it("betöltési hiba", async () => {
        mockApi({ "GET /api/admin/orders/12": { status: 500 } });
        renderPage();
        expect(await screen.findByText("Failed to load order details. Please try again.")).toBeInTheDocument();
    });
});
