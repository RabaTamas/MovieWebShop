import { act, screen } from "@testing-library/react";

import Auctions from "./Auctions";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const auctions = [
    { id: 1, title: "Live poster", status: 1, currentPrice: 8000, endsAt: new Date(Date.now() + 3600_000).toISOString(), imageUrl: "/a.jpg", currentBidderName: "trinity" },
    { id: 2, title: "Soon prop", status: 0, currentPrice: 5000, startsAt: "2030-01-01T10:00:00Z" },
    { id: 3, title: "Old reel", status: 2, currentPrice: 9000 },
];

describe("Auctions oldal", () => {
    it("listázza az aukciókat állapot szerint, és 30 mp-enként frissít", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        try {
            const routes = { "GET /api/Auction": auctions };
            const api = mockApi(routes);
            renderWithProviders(<Auctions />);

            expect(await screen.findByText("Live poster")).toBeInTheDocument();
            expect(screen.getByText("Place a bid")).toBeInTheDocument();
            expect(screen.getByText("Coming soon")).toBeInTheDocument();
            expect(screen.getByText("View result")).toBeInTheDocument();
            expect(screen.getByText("trinity")).toBeInTheDocument();
            expect(screen.queryByText("Your Won Auctions")).not.toBeInTheDocument();
            expect(api.find("GET", "/api/Auction/my-wins")).toHaveLength(0);

            routes["GET /api/Auction"] = [{ ...auctions[2], title: "Fresh reel" }];
            await act(async () => vi.advanceTimersByTimeAsync(30_000));
            expect(await screen.findByText("Fresh reel")).toBeInTheDocument();
        } finally {
            vi.useRealTimers();
        }
    });

    it("bejelentkezve a megnyert aukciókat is mutatja", async () => {
        loginAs();
        mockApi({
            "GET /api/Auction": auctions,
            "GET /api/Auction/my-wins": [
                { id: 3, title: "Won paid", currentPrice: 9000, isPaid: true },
                { id: 4, title: "Won unpaid", currentPrice: 7000, isPaid: false },
            ],
        });
        renderWithProviders(<Auctions />);

        expect(await screen.findByText("Your Won Auctions")).toBeInTheDocument();
        expect(screen.getByText("Pay now")).toBeInTheDocument();
        expect(screen.getAllByText("Paid").length).toBeGreaterThan(0);
    });

    it("üres lista; hibás betöltés", async () => {
        loginAs();
        const routes = { "GET /api/Auction": [], "GET /api/Auction/my-wins": { status: 500 } };
        mockApi(routes);
        const first = renderWithProviders(<Auctions />);
        expect(await screen.findByText("No auctions right now")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/Auction"] = new Error("down");
        routes["GET /api/Auction/my-wins"] = new Error("down");
        renderWithProviders(<Auctions />);
        expect(await screen.findByText("No auctions right now")).toBeInTheDocument();
    });
});
