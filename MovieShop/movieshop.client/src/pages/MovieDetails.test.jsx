import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import MovieDetails from "./MovieDetails";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const movie = { id: 7, title: "Matrix", description: "Neo wakes up", price: 3000, discountedPrice: 2400, imageUrl: "/m.jpg", categories: [{ id: 1, name: "Sci-Fi" }] };

const images = (n) => Array.from({ length: n }, (_, i) => ({ url: `/b${i}.jpg`, thumbnailUrl: `/t${i}.jpg` }));

const extras = {
    originalTitle: "The Matrix",
    releaseDate: "1999-03-31",
    runtime: 136,
    originalLanguage: "en",
    directors: ["Lana Wachowski", "Lilly Wachowski"],
    budget: 63000000,
    revenue: 0,
    voteAverage: 8.2,
    voteCount: 25000,
    genres: ["Action", "Science Fiction"],
    tagline: "Welcome to the Real World",
    backdropUrl: "/backdrop.jpg",
    cast: [
        { name: "Keanu Reeves", character: "Neo", profileUrl: "/k.jpg" },
        { name: "Extra", character: null, profileUrl: null },
    ],
    backdrops: images(10),
    posters: images(3),
};

const renderPage = () =>
    renderWithProviders(<MovieDetails />, {
        route: "/movies/7",
        path: "/movies/:id",
        routes: (
            <>
                <Route path="/" element={<p>home page</p>} />
                <Route path="/login" element={<p>login page</p>} />
                <Route path="/cart" element={<p>cart page</p>} />
                <Route path="/my-movies" element={<p>my movies</p>} />
            </>
        ),
    });

describe("MovieDetails oldal", () => {
    it("teljes adatlap TMDB-adatokkal, galériával", async () => {
        mockApi({ "GET /api/Movie/7": movie, "GET /api/Movie/7/tmdb": extras, "GET /api/Review/movie/7": [] });
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByRole("heading", { level: 1, name: "Matrix" })).toBeInTheDocument();
        expect(await screen.findByText("Keanu Reeves")).toBeInTheDocument();
        expect(screen.getByText("“Welcome to the Real World”")).toBeInTheDocument();
        expect(screen.getByText("2h 16m")).toBeInTheDocument();
        expect(screen.getByText("-20%")).toBeInTheDocument();
        expect(screen.getByText("$63,000,000")).toBeInTheDocument();
        expect(screen.queryByText("Box office")).not.toBeInTheDocument();
        expect(screen.getByText("The Matrix")).toBeInTheDocument();
        expect(screen.getByText("+3")).toBeInTheDocument();
        await waitFor(() => expect(screen.getByText("8.2")).toBeInTheDocument(), { timeout: 3000 });

        // Galéria-nagyító: lapozás gombbal, billentyűvel, bélyegképpel
        await user.click(screen.getByRole("button", { name: "Open image 2 of 10" }));
        const dialog = await screen.findByRole("dialog");
        expect(within(dialog).getByText("2 / 10")).toBeInTheDocument();
        await user.click(within(dialog).getByRole("button", { name: "Next image" }));
        expect(within(dialog).getByText("3 / 10")).toBeInTheDocument();
        await user.keyboard("{ArrowLeft}");
        expect(within(dialog).getByText("2 / 10")).toBeInTheDocument();
        await user.keyboard("{ArrowRight}");
        await user.click(within(dialog).getByRole("button", { name: "Previous image" }));
        await user.click(within(dialog).getByRole("button", { name: "Show image 10" }));
        expect(within(dialog).getByText("10 / 10")).toBeInTheDocument();
        await user.click(within(dialog).getByRole("button", { name: "Show image 1" }));
        await user.click(within(dialog).getByRole("button", { name: "Previous image" }));
        expect(within(dialog).getByText("10 / 10")).toBeInTheDocument();
        await user.click(within(dialog).getByRole("button", { name: "Close gallery" }));
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

        await user.click(screen.getByRole("button", { name: "Open image 1 of 10" }));
        await screen.findByRole("dialog");
        await user.keyboard("{Escape}");
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    });

    it("vendéget a belépésre irányít a kosárba tételnél", async () => {
        mockApi({ "GET /api/Movie/7": { ...movie, discountedPrice: null, imageUrl: null }, "GET /api/Movie/7/tmdb": { status: 404 } });
        const user = userEvent.setup();
        renderPage();

        await user.click(await screen.findByRole("button", { name: /Add to Cart/ }));
        expect(screen.getByText("login page")).toBeInTheDocument();
    });

    it("bejelentkezve kosárba tesz; hibánál toast", async () => {
        loginAs();
        const routes = {
            "GET /api/Movie/7": { ...movie, tmdbInfo: { voteAverage: 4, voteCount: 10, releaseDate: "2000-01-01" } },
            "GET /api/Movie/7/tmdb": new Error("down"),
            "GET /api/Movie/purchased": [],
            "POST /api/ShoppingCart/add": { status: 400, body: { message: "Already in cart" } },
        };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("TMDB Rating")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Add to Cart/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Already in cart"));
        routes["POST /api/ShoppingCart/add"] = { status: 400, body: {} };
        await user.click(screen.getByRole("button", { name: /Add to Cart/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to add movie to cart"));

        routes["POST /api/ShoppingCart/add"] = {};
        await user.click(screen.getByRole("button", { name: /Add to Cart/ }));
        expect(await screen.findByText("cart page")).toBeInTheDocument();
        expect(api.find("POST", "/api/ShoppingCart/add").at(-1).body).toEqual({ movieId: 7, quantity: 1 });
    });

    it("már megvásárolt filmnél a lejátszásra mutat", async () => {
        loginAs();
        mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/tmdb": { ...extras, cast: [], backdrops: [], posters: [], originalTitle: "Matrix", directors: [] },
            "GET /api/Movie/purchased": [{ id: 7 }],
        });
        renderPage();

        expect(await screen.findByRole("link", { name: /Watch/ })).toHaveAttribute("href", "/my-movies/7/watch");
        expect(screen.getByText("Movie facts")).toBeInTheDocument();
    });

    it("hibás filmazonosítónál a főoldalra navigál, offline az offline-képernyőt mutatja", async () => {
        const routes = { "GET /api/Movie/7": { status: 404 }, "GET /api/Movie/7/tmdb": { status: 404 } };
        mockApi(routes);
        const first = renderPage();
        expect(await screen.findByText("home page")).toBeInTheDocument();
        first.unmount();

        vi.spyOn(navigator, "onLine", "get").mockReturnValue(false);
        routes["GET /api/Movie/7"] = new Error("offline");
        renderPage();
        expect(await screen.findByText("You are offline")).toBeInTheDocument();
    });

    it("a vásárlási előzmény hibája nem akasztja meg az oldalt", async () => {
        loginAs();
        mockApi({ "GET /api/Movie/7": movie, "GET /api/Movie/7/tmdb": { status: 404 }, "GET /api/Movie/purchased": new Error("x") });
        renderPage();
        expect(await screen.findByRole("button", { name: /Add to Cart/ })).toBeInTheDocument();
    });
});
