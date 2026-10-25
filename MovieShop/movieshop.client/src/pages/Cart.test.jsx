import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import Cart from "./Cart";
import { stripe, toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const items = [
    { movieId: 1, title: "Matrix", priceAtOrder: 2000 },
    { movieId: 2, title: "Alien", priceAtOrder: 3000 },
];
const address = { id: 9, street: "Fő utca 1", city: "Budapest", zip: "1111" };

const baseRoutes = (extra = {}) => ({
    "GET /api/Payment/config": { publishableKey: "pk_test" },
    "GET /api/ShoppingCart": { items },
    "GET /api/Address": [address],
    ...extra,
});

const fillAddress = async (user) => {
    await user.type(screen.getByLabelText("Street, house number"), "Kossuth tér 2");
    await user.type(screen.getByLabelText("City"), "Szeged");
    await user.type(screen.getByLabelText("Zip"), "6720");
    await user.click(screen.getByRole("button", { name: "Save" }));
};

describe("Cart oldal", () => {
    beforeEach(() => loginAs());

    it("üres kosár", async () => {
        mockApi(baseRoutes({ "GET /api/ShoppingCart": {} }));
        renderWithProviders(<Cart />);
        expect(await screen.findByText("Your cart is empty.")).toBeInTheDocument();
    });

    it("hibás betöltésnél is üres kosarat mutat", async () => {
        mockApi({ "GET /api/Payment/config": new Error("x"), "GET /api/ShoppingCart": new Error("x"), "GET /api/Address": new Error("x") });
        renderWithProviders(<Cart />);
        expect(await screen.findByText("Your cart is empty.")).toBeInTheDocument();
    });

    it("teljes vásárlás: fizetés indítása, kártyás fizetés, rendelés leadása", async () => {
        const routes = baseRoutes({
            "POST /api/Payment/create-payment-intent": { clientSecret: "cs_1" },
            "POST /api/Order": { id: 42 },
        });
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<Cart />);

        expect(await screen.findByText("2 items in your cart")).toBeInTheDocument();
        await waitFor(() => expect(screen.getByLabelText("City")).toHaveValue("Budapest"));
        expect(screen.getAllByText(/5\s?000 Ft/).length).toBeGreaterThan(0);

        await user.click(screen.getByRole("button", { name: /Proceed to Payment/ }));
        expect(api.find("POST", "/api/Payment/create-payment-intent")[0].body).toEqual({ amount: 5000 });
        expect(await screen.findByTestId("card-element")).toBeInTheDocument();

        routes["GET /api/ShoppingCart"] = { items: [] };
        await user.click(screen.getByRole("button", { name: /^Pay / }));
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Sikeres rendelés! Rendelés azonosító: 42", expect.anything()));
        expect(api.find("POST", "/api/Order")[0].body).toEqual({
            billingAddress: { city: "Budapest", street: "Fő utca 1", zip: "1111" },
            paymentIntentId: "pi_test",
            movies: [
                { movieId: 1, title: "Matrix", priceAtOrder: 2000 },
                { movieId: 2, title: "Alien", priceAtOrder: 3000 },
            ],
        });
        expect(await screen.findByText("Your cart is empty.")).toBeInTheDocument();

        // A toast „My Movies" gombja a saját filmekhez navigál
        const assign = vi.fn();
        vi.stubGlobal("location", { ...window.location, assign });
        toast.success.mock.calls[0][1].action.onClick();
        expect(assign).toHaveBeenCalledWith("/my-movies");
    });

    it("fizetési hibák és a fizetés megszakítása", async () => {
        const routes = baseRoutes({
            "POST /api/Payment/create-payment-intent": { status: 400, body: { error: "Amount too small" } },
            "POST /api/Order": { status: 500 },
        });
        mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<Cart />);
        await waitFor(() => expect(screen.getByLabelText("City")).toHaveValue("Budapest"));

        const proceed = screen.getByRole("button", { name: /Proceed to Payment/ });
        await user.click(proceed);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Server error: Amount too small"));
        routes["POST /api/Payment/create-payment-intent"] = { status: 400, body: {} };
        await user.click(proceed);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Server error: Unknown error"));
        routes["POST /api/Payment/create-payment-intent"] = new Error("offline");
        await user.click(proceed);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment initiation error: offline"));

        routes["POST /api/Payment/create-payment-intent"] = { clientSecret: "cs" };
        await user.click(proceed);
        await screen.findByTestId("card-element");

        stripe.paymentMethodResult = { error: { message: "Card declined" } };
        await user.click(screen.getByRole("button", { name: /^Pay / }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment error: Card declined"));

        stripe.paymentMethodResult = { paymentMethod: { id: "pm" } };
        stripe.confirmResult = { error: { message: "Insufficient funds" } };
        await user.click(screen.getByRole("button", { name: /^Pay / }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment error: Insufficient funds"));

        stripe.confirmResult = { paymentIntent: { id: "pi", status: "succeeded" } };
        await user.click(screen.getByRole("button", { name: /^Pay / }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Error occurred: Order failed."));

        // Nem „succeeded" státusznál nem ad le rendelést
        stripe.confirmResult = { paymentIntent: { id: "pi", status: "processing" } };
        await user.click(screen.getByRole("button", { name: /^Pay / }));

        await user.click(screen.getByRole("button", { name: "Cancel Payment" }));
        expect(screen.queryByTestId("card-element")).not.toBeInTheDocument();
    });

    it("tételt töröl; hibánál jelez", async () => {
        const routes = baseRoutes({ "DELETE /api/ShoppingCart/remove/1": {} });
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<Cart />);
        await screen.findByText("Matrix");

        routes["GET /api/ShoppingCart"] = { items: [items[1]] };
        await user.click(screen.getAllByRole("button", { name: "Remove" })[0]);
        await waitFor(() => expect(screen.queryByText("Matrix")).not.toBeInTheDocument());
        expect(api.find("DELETE", "/api/ShoppingCart/remove/1")).toHaveLength(1);

        await user.click(screen.getByRole("button", { name: "Remove" }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Error removing item"));
    });

    it("cím nélkül nem fizethet; új címet ment, hibánál jelez", async () => {
        const routes = baseRoutes({ "GET /api/Address": [], "POST /api/Address": { id: 10, street: "Kossuth tér 2", city: "Szeged", zip: "6720" } });
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<Cart />);

        expect(await screen.findByText("Save a billing address to continue.")).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Proceed to Payment/ })).toBeDisabled();

        await fillAddress(user);
        await waitFor(() => expect(screen.queryByText("Save a billing address to continue.")).not.toBeInTheDocument());
        expect(api.find("POST", "/api/Address")[0].body).toMatchObject({ city: "Szeged", zip: "6720" });

        // Meglévő cím módosítása PUT-tal; hibánál toast
        routes["PUT /api/Address/10"] = { status: 500 };
        await user.click(screen.getByRole("button", { name: "Save" }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to save billing address"));
    });
});
