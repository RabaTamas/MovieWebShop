import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AuctionDetail from "./AuctionDetail";
import { signalr, stripe, toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const inOneHour = () => new Date(Date.now() + 3600_000).toISOString();

const auction = (extra = {}) => ({
    id: 3,
    title: "Matrix poster",
    imageUrl: "/p.jpg",
    startingPrice: 5000,
    currentPrice: 8000,
    minBidIncrement: 1000,
    status: 1,
    endsAt: inOneHour(),
    startsAt: inOneHour(),
    currentBidderName: "trinity",
    currentBidderId: 2,
    recentBids: [
        { id: 2, bidderName: "trinity", amount: 8000, placedAt: "2030-01-01T10:00:00Z" },
        { id: 1, bidderName: "morpheus", amount: 6000, placedAt: "2030-01-01T09:00:00Z" },
    ],
    ...extra,
});

const renderPage = () => renderWithProviders(<AuctionDetail />, { route: "/auctions/3", path: "/auctions/:id" });

describe("AuctionDetail oldal", () => {
    it("élő aukción licitál, és fogadja a valós idejű eseményeket", async () => {
        loginAs({ id: 1 });
        const api = mockApi({
            "GET /api/Auction/3": auction(),
            "POST /api/Auction/3/bid": (req) =>
                req.body.amount === 10000 ? { antiSnipingTriggered: true } : { status: 400, body: { error: "Too low" } },
        });
        const user = userEvent.setup();
        const { unmount } = renderPage();

        expect(await screen.findByText("Matrix poster")).toBeInTheDocument();
        expect(screen.getByText("Live")).toBeInTheDocument();
        expect(screen.getByText("morpheus")).toBeInTheDocument();
        await waitFor(() => expect(signalr.last().invocations).toContainEqual(["JoinAuction", 3]));
        expect(signalr.last().options.accessTokenFactory()).toBe(localStorage.getItem("jwt"));

        const raise = screen.getByLabelText(/Raise by/);
        await user.clear(raise);
        await user.type(raise, "2000");
        expect(screen.getByText(/^10\s000 Ft$/, { selector: "strong" })).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Place Bid/ }));
        expect(await screen.findByText(/Bid of 10\D000 Ft placed successfully! Auction extended/)).toBeInTheDocument();
        expect(api.find("POST", "/api/Auction/3/bid")[0].body).toEqual({ amount: 10000 });

        await user.clear(raise);
        await user.type(raise, "1500");
        await user.click(screen.getByRole("button", { name: /Place Bid/ }));
        expect(await screen.findByText("Too low")).toBeInTheDocument();

        // Túl kicsi emelésnél a gomb letiltva
        await user.clear(raise);
        await user.type(raise, "10");
        expect(screen.getByRole("button", { name: /Place Bid/ })).toBeDisabled();

        act(() => signalr.emit("BidPlaced", { bidderName: "smith", amount: 12000, newEndsAt: inOneHour(), antiSniping: true, currentPrice: 12000 }));
        expect(screen.getByText(/smith bid 12\D000 Ft — Auction extended!/)).toBeInTheDocument();
        act(() => signalr.emit("BidPlaced", { bidderName: "neo", amount: 13000, newEndsAt: inOneHour(), antiSniping: false, currentPrice: 13000 }));
        expect(screen.getByText(/^neo bid 13\D000 Ft$/)).toBeInTheDocument();
        expect(screen.getByText(/^13\s000 Ft$/, { selector: "div" })).toBeInTheDocument();

        act(() => signalr.emit("AuctionEnded", { winnerName: "neo", finalPrice: 13000, movieTitle: "Matrix" }));
        expect(screen.getByText(/^Auction ended! neo won "Matrix" for 13\D000 Ft$/)).toBeInTheDocument();
        expect(screen.getAllByText("Ended").length).toBeGreaterThan(0);

        unmount();
        expect(signalr.last().invocations).toContainEqual(["LeaveAuction", 3]);
    });

    it("licitnél hálózati hiba és érvénytelen emelés", async () => {
        loginAs({ id: 1 });
        mockApi({ "GET /api/Auction/3": auction({ minBidIncrement: null }), "POST /api/Auction/3/bid": new Error("down") });
        const user = userEvent.setup();
        renderPage();

        const raise = await screen.findByLabelText(/Raise by/);
        expect(raise).toHaveValue(1000);
        await user.click(screen.getByRole("button", { name: /Place Bid/ }));
        expect(await screen.findByText("Network error. Please try again.")).toBeInTheDocument();

        fireEvent.change(raise, { target: { value: "" } });
        await user.click(screen.getByRole("button", { name: /Place Bid/ }));
        expect(await screen.findByText(/^Minimum raise is 1\D?000 Ft\.$/)).toBeInTheDocument();
    });

    it("szerverhiba üzenet nélkül: alapértelmezett szöveg; vendégnek belépési link", async () => {
        loginAs({ id: 1 });
        mockApi({ "GET /api/Auction/3": auction(), "POST /api/Auction/3/bid": { status: 400, body: {} } });
        const user = userEvent.setup();
        renderPage();
        await user.click(await screen.findByRole("button", { name: /Place Bid/ }));
        expect(await screen.findByText("Bid failed.")).toBeInTheDocument();
    });

    it("kijelentkezve nem lehet licitálni", async () => {
        mockApi({ "GET /api/Auction/3": auction({ recentBids: [] }) });
        renderPage();

        expect(await screen.findByRole("link", { name: "Log in" })).toBeInTheDocument();
        expect(screen.getByText("No bids yet. Be the first!")).toBeInTheDocument();
        expect(signalr.last().options).toEqual({ skipNegotiation: false });
    });

    it("hamarosan induló és licit nélkül lezárt aukció", async () => {
        mockApi({ "GET /api/Auction/3": auction({ status: 0 }) });
        renderPage();
        expect(await screen.findByText("Starting soon")).toBeInTheDocument();
        expect(screen.getByText("Starts at")).toBeInTheDocument();
    });

    it("lezárt aukció: nyertes neve, illetve licit nélküli lezárás", async () => {
        loginAs({ id: 1 });
        mockApi({ "GET /api/Auction/3": auction({ status: 2 }) });
        const { unmount } = renderPage();
        expect(await screen.findByText(/Winner: trinity/)).toBeInTheDocument();
        unmount();

        mockApi({ "GET /api/Auction/3": auction({ status: 2, currentBidderName: null, currentBidderId: null }) });
        renderPage();
        expect(await screen.findByText("Ended with no bids")).toBeInTheDocument();
        act(() => signalr.emit("AuctionEnded", { winnerName: null }));
        expect(screen.getByText("Auction ended with no bids.")).toBeInTheDocument();
    });

    it("a nyertes Stripe-pal fizet", async () => {
        loginAs({ id: 2 });
        const api = mockApi({
            "GET /api/Auction/3": auction({ status: 2 }),
            "POST /api/Auction/3/create-payment": { publishableKey: "pk_test", clientSecret: "cs_1" },
            "POST /api/Auction/3/confirm-payment": {},
        });
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("You won this auction!")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Pay 8\s?000 Ft/ }));
        expect(await screen.findByTestId("card-element")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Pay 8\s?000 Ft/ }));

        expect(await screen.findByText(/Payment complete/)).toBeInTheDocument();
        expect(api.find("POST", "/api/Auction/3/confirm-payment")[0].body).toEqual({ paymentIntentId: "pi_test" });
    });

    it("fizetési hibák: nem elérhető, Stripe-hiba, kártyahiba, szerveroldali ellenőrzés", async () => {
        loginAs({ id: 2 });
        const routes = {
            "GET /api/Auction/3": auction({ status: 2 }),
            "POST /api/Auction/3/create-payment": { status: 400 },
            "POST /api/Auction/3/confirm-payment": { status: 500 },
        };
        mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        const pay = await screen.findByRole("button", { name: /Pay 8\s?000 Ft/ });
        await user.click(pay);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment not available."));

        routes["POST /api/Auction/3/create-payment"] = new Error("down");
        await user.click(pay);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Network error."));

        routes["POST /api/Auction/3/create-payment"] = { publishableKey: "pk", clientSecret: "cs" };
        await user.click(pay);
        await screen.findByTestId("card-element");

        stripe.paymentMethodResult = { error: { message: "Card declined" } };
        await user.click(screen.getByRole("button", { name: /Pay 8\s?000 Ft/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment error: Card declined"));

        stripe.paymentMethodResult = { paymentMethod: { id: "pm_1" } };
        stripe.confirmResult = { error: { message: "3DS failed" } };
        await user.click(screen.getByRole("button", { name: /Pay 8\s?000 Ft/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Payment failed: 3DS failed"));

        stripe.confirmResult = { paymentIntent: { id: "pi_2", status: "succeeded" } };
        await user.click(screen.getByRole("button", { name: /Pay 8\s?000 Ft/ }));
        await waitFor(() =>
            expect(toast.error).toHaveBeenCalledWith("Payment confirmed by Stripe but server verification failed.")
        );
    });

    it("már kifizetett nyertes aukció", async () => {
        loginAs({ id: 2 });
        mockApi({ "GET /api/Auction/3": auction({ status: 2, isPaid: true }) });
        renderPage();
        expect(await screen.findByText(/Payment complete/)).toBeInTheDocument();
    });

    it("nem létező aukció és sikertelen hub-kapcsolat", async () => {
        signalr.failStart = true;
        mockApi({ "GET /api/Auction/3": { status: 404 } });
        renderPage();
        expect(await screen.findByText("Auction not found")).toBeInTheDocument();
    });

    it("hálózati hiba betöltéskor", async () => {
        mockApi({ "GET /api/Auction/3": new Error("down") });
        renderPage();
        expect(await screen.findByText("Auction not found")).toBeInTheDocument();
    });
});
