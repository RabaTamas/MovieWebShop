import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import MyWonAuctions from "./MyWonAuctions";
import { stripe, toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const wins = [
    { id: 1, title: "Ring prop", currentPrice: 9000, imageUrl: "/r.jpg", isPaid: false },
    { id: 2, title: "Old reel", currentPrice: 4000, imageUrl: "/o.jpg", isPaid: true },
];

describe("MyWonAuctions oldal", () => {
    beforeEach(() => loginAs());

    it("kifizeti a megnyert aukciót", async () => {
        const api = mockApi({
            "GET /api/Auction/my-wins": wins,
            "POST /api/Auction/1/create-payment": { publishableKey: "pk", clientSecret: "cs_1" },
            "POST /api/Auction/1/confirm-payment": {},
        });
        const user = userEvent.setup();
        renderWithProviders(<MyWonAuctions />);

        expect(await screen.findByText(/You have 1 unpaid auction\./)).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Pay 9\s?000 Ft/ }));
        expect(await screen.findByText("Complete your payment")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /^Pay / }));

        expect(await screen.findByText("All your won auctions are paid. Enjoy your movies!")).toBeInTheDocument();
        expect(screen.getAllByText(/Payment complete/)).toHaveLength(2);
        expect(api.find("POST", "/api/Auction/1/confirm-payment")[0].body).toEqual({ paymentIntentId: "pi_test" });
    });

    it("fizetési hibák és megszakítás", async () => {
        const routes = {
            "GET /api/Auction/my-wins": [wins[0], { ...wins[0], id: 3, title: "Second" }],
            "POST /api/Auction/1/create-payment": { status: 400 },
            "POST /api/Auction/1/confirm-payment": { status: 500 },
        };
        mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<MyWonAuctions />);

        expect(await screen.findByText(/You have 2 unpaid auctions\./)).toBeInTheDocument();
        const [pay] = screen.getAllByRole("button", { name: /Pay 9\s?000 Ft/ });
        await user.click(pay);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment not available for this auction."));
        routes["POST /api/Auction/1/create-payment"] = new Error("down");
        await user.click(pay);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Network error."));

        routes["POST /api/Auction/1/create-payment"] = { publishableKey: "pk", clientSecret: "cs" };
        await user.click(pay);
        await screen.findByText("Complete your payment");

        // A kártyás űrlap saját „Pay" gombja (a második aukció fizetés-gombja mellett)
        const submit = () => user.click(screen.getByText("Complete your payment").parentElement.querySelector("button[type=submit]"));
        stripe.paymentMethodResult = { error: { message: "Declined" } };
        await submit();
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment error: Declined"));

        stripe.paymentMethodResult = { paymentMethod: { id: "pm" } };
        stripe.confirmResult = { error: { message: "3DS" } };
        await submit();
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment failed: 3DS"));

        stripe.confirmResult = { paymentIntent: { id: "pi", status: "succeeded" } };
        await submit();
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment confirmed by Stripe but server verification failed."));

        stripe.confirmResult = { paymentIntent: { id: "pi", status: "processing" } };
        await submit();

        await user.click(screen.getByRole("button", { name: "Cancel" }));
        expect(screen.queryByText("Complete your payment")).not.toBeInTheDocument();
    });

    it("nincs megnyert aukció; hibás betöltés", async () => {
        const routes = { "GET /api/Auction/my-wins": { status: 500 } };
        mockApi(routes);
        const first = renderWithProviders(<MyWonAuctions />);
        expect(await screen.findByText("No won auctions yet")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/Auction/my-wins"] = new Error("down");
        renderWithProviders(<MyWonAuctions />);
        expect(await screen.findByText("No won auctions yet")).toBeInTheDocument();
    });
});
