import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminAuctions from "./AdminAuctions";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const auctions = [
    { id: 1, title: "Ring prop", status: 0, currentPrice: 5000, startingPrice: 5000, endsAt: "2030-01-02T10:00:00Z", startsAt: "2030-01-01T10:00:00Z", imageUrl: "/r.jpg", movieId: 7, description: "Gold" },
    { id: 2, title: "Live one", status: 1, currentPrice: 8000, startingPrice: 6000, endsAt: "2030-01-02T10:00:00Z", startsAt: "2030-01-01T10:00:00Z" },
];

const row = (title) => screen.getByText(title, { selector: "td" }).closest("tr");

const fillDates = () => {
    fireEvent.change(screen.getByLabelText("Starts At"), { target: { value: "2030-05-01T10:00" } });
    fireEvent.change(screen.getByLabelText("Ends At"), { target: { value: "2030-05-02T10:00" } });
};

describe("AdminAuctions oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("aukciót hoz létre", async () => {
        const routes = {
            "GET /api/Auction/all": [],
            "GET /api/Movie": { items: [{ id: 7, title: "Matrix" }] },
            "POST /api/Auction": { id: 3, title: "Matrix poster", status: 0, currentPrice: 1000, endsAt: "2030-05-02T10:00:00Z" },
        };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<AdminAuctions />);

        expect(await screen.findByText("No auctions yet.")).toBeInTheDocument();
        await user.selectOptions(await screen.findByLabelText("Movie (optional)"), "7");
        await user.type(screen.getByLabelText("Auction Title"), "Matrix poster");
        await user.type(screen.getByLabelText("Image URL"), "https://img/p.jpg");
        expect(screen.getByAltText("preview")).toHaveAttribute("src", "https://img/p.jpg");
        fillDates();
        await user.type(screen.getByLabelText("Starting Price (Ft)"), "1000");
        await user.click(screen.getByRole("button", { name: /Create Auction/ }));

        expect(await screen.findByText('Auction "Matrix poster" created!')).toBeInTheDocument();
        expect(screen.getByText("Matrix poster", { selector: "td" })).toBeInTheDocument();
        expect(api.find("POST", "/api/Auction")[0].body).toMatchObject({
            movieId: 7, auctionTitle: "Matrix poster", description: null, imageUrl: "https://img/p.jpg", startingPrice: 1000,
        });

        // Szerverhiba és hálózati hiba
        routes["POST /api/Auction"] = { status: 400, body: "Ends before start" };
        fillDates();
        await user.type(screen.getByLabelText("Starting Price (Ft)"), "1");
        await user.click(screen.getByRole("button", { name: /Create Auction/ }));
        expect(await screen.findByText("Ends before start")).toBeInTheDocument();
        routes["POST /api/Auction"] = new Error("down");
        await user.click(screen.getByRole("button", { name: /Create Auction/ }));
        expect(await screen.findByText("Network error.")).toBeInTheDocument();
    });

    it("aukciót szerkeszt; aktív aukció ára zárolt", async () => {
        const routes = {
            "GET /api/Auction/all": auctions,
            "GET /api/Movie": [{ id: 7, title: "Matrix" }],
            "PUT /api/Auction/1": { ...auctions[0], title: "Ring prop v2" },
        };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<AdminAuctions />);

        await user.click(within(await screen.findByRole("row", { name: /Live one/ })).getByRole("button", { name: /Edit/ }));
        expect(screen.getByText("Edit: Live one")).toBeInTheDocument();
        expect(screen.getByLabelText("Starting Price (Ft)")).toBeDisabled();
        expect(screen.getByText("(locked — auction active)")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Cancel edit/ }));
        expect(screen.getByText("Create New Auction")).toBeInTheDocument();

        await user.click(within(row("Ring prop")).getByRole("button", { name: /Edit/ }));
        expect(screen.getByLabelText("Auction Title")).toHaveValue("Ring prop");
        expect(screen.getByLabelText("Starts At")).toHaveValue("2030-01-01T10:00");
        expect(screen.getByLabelText("Starting Price (Ft)")).toBeEnabled();
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        expect(await screen.findByText("Auction updated!")).toBeInTheDocument();
        expect(screen.getByText("Ring prop v2", { selector: "td" })).toBeInTheDocument();
        expect(api.find("PUT", "/api/Auction/1")[0].body).toMatchObject({ movieId: 7, auctionTitle: "Ring prop", description: "Gold" });

        // Szerkesztési hibák
        routes["PUT /api/Auction/2"] = { status: 409, body: "Already ended" };
        await user.click(within(row("Live one")).getByRole("button", { name: /Edit/ }));
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        expect(await screen.findByText("Already ended")).toBeInTheDocument();
        routes["PUT /api/Auction/2"] = new Error("down");
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        expect(await screen.findByText("Network error.")).toBeInTheDocument();
    });

    it("megerősítés után töröl; megszakítható", async () => {
        const routes = { "GET /api/Auction/all": auctions, "GET /api/Movie": [], "DELETE /api/Auction/1": {} };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderWithProviders(<AdminAuctions />);

        await user.click(within(await screen.findByRole("row", { name: /Ring prop/ })).getByRole("button", { name: /Delete/ }));
        let dialog = await screen.findByRole("dialog");
        await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

        await user.click(within(row("Ring prop")).getByRole("button", { name: /Delete/ }));
        dialog = await screen.findByRole("dialog");
        await user.click(within(dialog).getByRole("button", { name: /Delete/ }));
        await waitFor(() => expect(screen.queryByText("Ring prop", { selector: "td" })).not.toBeInTheDocument());
        expect(api.find("DELETE", "/api/Auction/1")).toHaveLength(1);

        // Hálózati hibánál is bezárul a párbeszédablak
        routes["DELETE /api/Auction/2"] = new Error("down");
        await user.click(within(row("Live one")).getByRole("button", { name: /Delete/ }));
        dialog = await screen.findByRole("dialog");
        await user.click(within(dialog).getByRole("button", { name: /Delete/ }));
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
        expect(screen.getByText("Live one", { selector: "td" })).toBeInTheDocument();

        await user.click(within(row("Live one")).getByRole("button", { name: /Delete/ }));
        await user.keyboard("{Escape}");
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    });

    it("sikertelen listázásnál üres táblát mutat", async () => {
        mockApi({ "GET /api/Auction/all": { status: 500 }, "GET /api/Movie": [] });
        renderWithProviders(<AdminAuctions />);
        expect(await screen.findByText("No auctions yet.")).toBeInTheDocument();
    });
});
