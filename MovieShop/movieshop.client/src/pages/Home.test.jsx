import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import Home from "./Home";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const movies = [
    { id: 1, title: "Matrix", price: 1000, imageUrl: "/m.jpg" },
    { id: 2, title: "Alien", price: 3000, discountedPrice: 1500, imageUrl: "/a.jpg" },
    { id: 3, title: "Up", price: 2000, imageUrl: "/u.jpg" },
];
const categories = [
    { id: 10, name: "Action" },
    { id: 11, name: "Drama" },
];

const titles = () =>
    screen.getAllByRole("heading", { level: 3 }).filter((h) => h.closest("a")).map((h) => h.textContent);

describe("Home oldal", () => {
    it("listázza a filmeket és a kategóriákat, rendez és sűrűséget vált", async () => {
        const api = mockApi({ "GET /api/Movie": movies, "GET /api/Category": categories });
        const user = userEvent.setup();
        renderWithProviders(<Home />);

        expect(await screen.findByText("Matrix")).toBeInTheDocument();
        expect(screen.getAllByRole("checkbox", { name: "Action" }).length).toBeGreaterThan(0);
        expect(titles()).toEqual(["Matrix", "Alien", "Up"]);
        expect(api.find("GET", "/api/Recommendation")).toHaveLength(0);

        await user.click(screen.getByRole("button", { name: "Sort" }));
        await user.click(await screen.findByRole("menuitemcheckbox", { name: "Name (A-Z)" }));
        expect(titles()).toEqual(["Alien", "Matrix", "Up"]);

        await user.click(screen.getByRole("button", { name: "Sort" }));
        await user.click(await screen.findByRole("menuitemcheckbox", { name: "Name (Z-A)" }));
        expect(titles()).toEqual(["Up", "Matrix", "Alien"]);

        await user.click(screen.getByRole("button", { name: "Sort" }));
        await user.click(await screen.findByRole("menuitemcheckbox", { name: /Low → High/ }));
        expect(titles()).toEqual(["Matrix", "Alien", "Up"]);

        await user.click(screen.getByRole("button", { name: "Sort" }));
        await user.click(await screen.findByRole("menuitemcheckbox", { name: /High → Low/ }));
        expect(titles()).toEqual(["Up", "Alien", "Matrix"]);
        expect(screen.queryByRole("menuitemcheckbox", { name: /Rating/ })).not.toBeInTheDocument();

        // A rendezés is „szűrő", ezért a Reset megjelenik és visszaállít
        await user.click(screen.getAllByRole("button", { name: "Reset" })[0]);
        expect(titles()).toEqual(["Matrix", "Alien", "Up"]);

        await user.click(screen.getByRole("button", { name: "Compact grid" }));
        expect(localStorage.getItem("movieshop-grid-density")).toBe("compact");
    });

    it("a kategória- és árszűrő chipként jelenik meg, és eltávolítható", async () => {
        const api = mockApi({
            "GET /api/Movie": movies,
            "GET /api/Category": categories,
            "GET /api/Movie/categories": [movies[0]],
        });
        const user = userEvent.setup();
        renderWithProviders(<Home />);
        await screen.findByText("Alien");

        await user.click(screen.getAllByRole("checkbox", { name: "Action" })[0]);
        await waitFor(() => expect(titles()).toEqual(["Matrix"]));
        expect(api.find("GET", "/api/Movie/categories")[0].query.getAll("categoryIds")).toEqual(["10"]);

        // Ismeretlen kategória-azonosító nem fordulhat elő, a chip a nevet mutatja
        const chip = screen.getByRole("button", { name: "Remove filter Action" });
        await user.click(chip);
        await waitFor(() => expect(titles()).toHaveLength(3));

        // Árszűrő a min/max mezőkkel (az effektív, kedvezményes ár számít)
        fireEvent.change(screen.getAllByLabelText("Min price")[0], { target: { value: "1200" } });
        fireEvent.change(screen.getAllByLabelText("Max price")[0], { target: { value: "2500" } });
        // A kilépő kártyát az AnimatePresence az animáció végéig még a DOM-ban tartja
        await waitFor(() => expect(titles()).toEqual(["Alien", "Up"]));
        const priceChip = screen.getByRole("button", { name: /Remove filter .*–/ });
        await user.click(priceChip);
        await waitFor(() => expect(titles()).toHaveLength(3));

        // Túl szigorú szűrő: üres állapot, „Reset filters" gombbal
        fireEvent.change(screen.getAllByLabelText("Min price")[0], { target: { value: "99999" } });
        expect(await screen.findByText("There are no movies matching your filters!")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Reset filters/ }));
        await waitFor(() => expect(titles()).toHaveLength(3));

        // „Clear all" a chipsorban
        fireEvent.change(screen.getAllByLabelText("Max price")[0], { target: { value: "2000" } });
        await user.click(screen.getByRole("button", { name: "Clear all" }));
        await waitFor(() => expect(titles()).toHaveLength(3));
    });

    it("az ár-csúszka billentyűzettel állítható", async () => {
        mockApi({ "GET /api/Movie": movies, "GET /api/Category": categories });
        renderWithProviders(<Home />);
        await screen.findByText("Alien");

        const [minThumb] = screen.getAllByRole("slider", { name: "Minimum price" });
        minThumb.focus();
        fireEvent.keyDown(minThumb, { key: "ArrowRight" });
        await waitFor(() => expect(screen.getAllByLabelText("Min price")[0].value).not.toBe(""));

        const [maxThumb] = screen.getAllByRole("slider", { name: "Maximum price" });
        maxThumb.focus();
        fireEvent.keyDown(maxThumb, { key: "ArrowLeft" });
        await waitFor(() => expect(screen.getAllByLabelText("Max price")[0].value).not.toBe(""));
    });

    it("lapoz, ha több film van egy oldalnál", async () => {
        const many = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, title: `Film ${i + 1}`, price: 100 * (i + 1) }));
        mockApi({ "GET /api/Movie": many, "GET /api/Category": [] });
        const user = userEvent.setup();
        renderWithProviders(<Home />);

        expect(await screen.findByText("Film 1")).toBeInTheDocument();
        expect(screen.queryByText("Film 9")).not.toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: "Next page" }));
        expect(screen.getByText("Film 9")).toBeInTheDocument();
    });

    it("keresés: javaslatok, találatok, lapozás és törlés", async () => {
        const api = mockApi({
            "GET /api/Movie": movies,
            "GET /api/Category": categories,
            "GET /api/Search/autocomplete": ["Matrix Reloaded"],
            "GET /api/Search": (req) => ({
                total: 9,
                movies: [{ id: 50 + Number(req.query.get("page")), title: `Hit page ${req.query.get("page")}`, price: 1 }],
            }),
        });
        const user = userEvent.setup();
        renderWithProviders(<Home />);
        await screen.findByText("Alien");

        await user.click(screen.getAllByRole("checkbox", { name: "Drama" })[0]);
        await user.type(screen.getByLabelText("Search"), "mat");

        expect(await screen.findByText("Hit page 1")).toBeInTheDocument();
        expect(screen.getAllByText(/result.? found/)[0]).toBeInTheDocument();
        const searchReq = api.find("GET", "/api/Search").find((c) => c.path === "/api/Search");
        expect(searchReq.query.get("q")).toBe("mat");
        expect(searchReq.query.getAll("categories")).toEqual(["Drama"]);

        // Javaslat kiválasztása
        const suggestion = await screen.findByText("Matrix Reloaded");
        fireEvent.mouseDown(suggestion);
        expect(screen.getByLabelText("Search")).toHaveValue("Matrix Reloaded");

        // Keresési módban a „Rating" rendezés is elérhető
        await user.click(screen.getByRole("button", { name: "Sort" }));
        await user.click(await screen.findByRole("menuitemcheckbox", { name: /Rating/ }));
        await waitFor(() =>
            expect(api.calls.some((c) => c.path === "/api/Search" && c.query.get("sort") === "rating-desc")).toBe(true)
        );

        fireEvent.change(screen.getAllByLabelText("Min price")[0], { target: { value: "5" } });
        fireEvent.change(screen.getAllByLabelText("Max price")[0], { target: { value: "50" } });
        await waitFor(() =>
            expect(api.calls.some((c) => c.path === "/api/Search" && c.query.get("maxPrice") === "50")).toBe(true)
        );

        await user.click(await screen.findByRole("button", { name: "Next page" }));
        expect(await screen.findByText("Hit page 2")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Clear search" }));
        expect(await screen.findByRole("heading", { name: "All movies" })).toBeInTheDocument();
    });

    it("üres keresési eredménynél üzenetet mutat, hibás válasznál nem omlik össze", async () => {
        mockApi({
            "GET /api/Movie": movies,
            "GET /api/Category": { status: 500 },
            "GET /api/Search/autocomplete": new Error("offline"),
            "GET /api/Search": { total: 0, movies: [] },
        });
        const user = userEvent.setup();
        renderWithProviders(<Home />);
        await screen.findByText("Alien");

        await user.type(screen.getByLabelText("Search"), "zzz");
        expect(await screen.findByText('No results for "zzz"')).toBeInTheDocument();
    });

    it("hálózati hiba és offline állapot esetén az offline képernyőt mutatja", async () => {
        mockApi({ "GET /api/Movie": new Error("down"), "GET /api/Category": new Error("down") });
        vi.spyOn(navigator, "onLine", "get").mockReturnValue(false);
        renderWithProviders(<Home />);

        expect(await screen.findByText("You are offline")).toBeInTheDocument();
    });

    it("bejelentkezett felhasználónak ajánlásokat hirdet", async () => {
        loginAs();
        const api = mockApi({
            "GET /api/Movie": movies,
            "GET /api/Category": categories,
            "GET /api/Recommendation": { categoryBased: [movies[0]], collaborativeBased: [] },
        });
        renderWithProviders(<Home />);

        expect(await screen.findByText(/personalised recommendations/)).toBeInTheDocument();
        expect(api.find("GET", "/api/Recommendation")[0].headers.Authorization).toMatch(/^Bearer /);
    });

    it("mobil szűrőpanel megnyitható és bezárható", async () => {
        mockApi({ "GET /api/Movie": movies, "GET /api/Category": categories });
        const user = userEvent.setup();
        renderWithProviders(<Home />);
        await screen.findByText("Alien");

        await user.click(screen.getByRole("button", { name: /^Filters/ }));
        const dialog = await screen.findByRole("dialog");
        await user.click(within(dialog).getByRole("checkbox", { name: "Action" }));
        await user.click(within(dialog).getByRole("button", { name: "Reset" }));
        await user.click(within(dialog).getByRole("button", { name: /Show \d+ movies?/ }));
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    });
});
