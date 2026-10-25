import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import MyMovies from "./MyMovies";
import Orders from "./Orders";
import Recommendations from "./Recommendations";
import Register from "./Register";
import AdminOrders from "./AdminOrders";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

describe("Recommendations oldal", () => {
    it("vendégnek belépést kér", () => {
        mockApi({});
        renderWithProviders(<Recommendations />);
        expect(screen.getByText("Log in to see your recommendations")).toBeInTheDocument();
    });

    it("ajánlásokat mutat, és az AI-chatbotnak továbbítja a látott címeket", async () => {
        loginAs();
        mockApi({
            "GET /api/Recommendation": {
                categoryBased: [{ id: 1, title: "Matrix", price: 1000, reason: "You like Sci-Fi" }],
                collaborativeBased: [{ id: 2, title: "Alien", price: 2000 }],
            },
        });
        const opened = vi.fn();
        window.addEventListener("chatbot:open", opened);
        const user = userEvent.setup();
        renderWithProviders(<Recommendations />);

        expect(await screen.findByText("Recommended for You")).toBeInTheDocument();
        expect(screen.getByText("Customers Also Bought")).toBeInTheDocument();
        expect(screen.getByText("You like Sci-Fi")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Ask AI for more suggestions/ }));
        expect(opened.mock.calls[0][0].detail.message).toMatch(/Matrix, Alien/);
        window.removeEventListener("chatbot:open", opened);
    });

    it("csak az egyik lista; üres és hibás válasz", async () => {
        loginAs();
        const routes = { "GET /api/Recommendation": { categoryBased: [], collaborativeBased: [{ id: 2, title: "Alien", price: 1 }] } };
        mockApi(routes);
        const first = renderWithProviders(<Recommendations />);
        expect(await screen.findByText("Customers Also Bought")).toBeInTheDocument();
        expect(screen.queryByText("Recommended for You")).not.toBeInTheDocument();
        first.unmount();

        routes["GET /api/Recommendation"] = { status: 500 };
        const second = renderWithProviders(<Recommendations />);
        expect(await screen.findByText("No recommendations yet")).toBeInTheDocument();
        second.unmount();

        routes["GET /api/Recommendation"] = new Error("down");
        renderWithProviders(<Recommendations />);
        expect(await screen.findByText("No recommendations yet")).toBeInTheDocument();
    });
});

describe("MyMovies oldal", () => {
    beforeEach(() => loginAs());

    it("a megvásárolt filmeket mutatja lejátszás- és Watch Party-linkkel", async () => {
        mockApi({
            "GET /api/Movie/purchased": [
                { id: 1, title: "Matrix", imageUrl: "/m.jpg", description: "Neo" },
                { id: 2, title: "Alien" },
            ],
        });
        renderWithProviders(<MyMovies />);

        expect(await screen.findByText("2 movies in your library")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: "Watch Matrix" })).toHaveAttribute("href", "/my-movies/1/watch");
        expect(screen.getAllByRole("link", { name: "Watch Party" })[1]).toHaveAttribute("href", "/my-movies/2/watch-party");
    });

    it("üres könyvtár és betöltési hiba", async () => {
        const routes = { "GET /api/Movie/purchased": [] };
        mockApi(routes);
        const first = renderWithProviders(<MyMovies />);
        expect(await screen.findByText("No movies purchased yet")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/Movie/purchased"] = { status: 500 };
        renderWithProviders(<MyMovies />);
        expect(await screen.findByText("Error: Failed to fetch purchased movies")).toBeInTheDocument();
    });
});

describe("Orders oldal", () => {
    beforeEach(() => loginAs());

    it("a felhasználó rendeléseit listázza", async () => {
        mockApi({
            "GET /api/Order/user": [
                { id: 5, orderDate: "2030-01-01T10:00:00Z", status: "Completed", totalPrice: 3000, billingAddress: { street: "Fő utca 1", city: "Budapest", zip: "1111" }, movies: [{ movieId: 1, title: "Matrix", priceAtOrder: 3000 }] },
                { id: 6, orderDate: "2030-01-02T10:00:00Z", status: "Pending", totalPrice: 1000, movies: [] },
            ],
        });
        renderWithProviders(<Orders />);

        expect(await screen.findByText("Order #5")).toBeInTheDocument();
        expect(screen.getByText("2 orders")).toBeInTheDocument();
        expect(screen.getByText("Fő utca 1, Budapest 1111")).toBeInTheDocument();
    });

    it("üres lista hibánál is", async () => {
        mockApi({ "GET /api/Order/user": { status: 500 } });
        renderWithProviders(<Orders />);
        expect(await screen.findByText("No orders yet")).toBeInTheDocument();
    });
});

describe("AdminOrders oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("listáz, állapot szerint szűr és a részletekre navigál", async () => {
        const order = { id: 9, orderDate: "2030-01-01", userName: "neo", userEmail: "neo@zion.io", totalPrice: 2000, status: "Pending" };
        const api = mockApi({
            "GET /api/admin/orders": [order],
            "GET /api/admin/orders/status/Completed": [],
        });
        const user = userEvent.setup();
        renderWithProviders(<AdminOrders />, { routes: <Route path="/admin/orders/:id" element={<p>details page</p>} />, route: "/admin/orders", path: "/admin/orders" });

        expect(await screen.findByText("#9")).toBeInTheDocument();
        await user.selectOptions(screen.getByLabelText("Filter by Status:"), "Completed");
        expect(await screen.findByText("No orders found.")).toBeInTheDocument();
        expect(api.find("GET", "/api/admin/orders/status/Completed")).toHaveLength(1);

        await user.selectOptions(screen.getByLabelText("Filter by Status:"), "");
        await user.click(await screen.findByRole("button", { name: /View Details/ }));
        expect(screen.getByText("details page")).toBeInTheDocument();
    });

    it("sorra kattintva is a részletekre navigál; hibánál üzenet", async () => {
        const routes = { "GET /api/admin/orders": [{ id: 9, orderDate: "2030-01-01", totalPrice: 1, status: "Failed" }] };
        mockApi(routes);
        const first = renderWithProviders(<AdminOrders />, { routes: <Route path="/admin/orders/:id" element={<p>details page</p>} />, route: "/admin/orders", path: "/admin/orders" });
        fireEvent.click(await screen.findByText("#9"));
        expect(screen.getByText("details page")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/admin/orders"] = { status: 500 };
        renderWithProviders(<AdminOrders />);
        expect(await screen.findByText("Failed to load orders. Please try again.")).toBeInTheDocument();
    });
});

describe("Register oldal", () => {
    const fill = async (user, { name = "Neo", email = "neo@zion.io", password = "Test123!", confirm = password } = {}) => {
        if (name) await user.type(screen.getByLabelText("Name"), name);
        if (email) await user.type(screen.getByLabelText("Email"), email);
        if (password) await user.type(screen.getByLabelText("Password"), password);
        if (confirm) await user.type(screen.getByLabelText("Confirm Password"), confirm);
    };
    const submit = () => fireEvent.submit(screen.getByRole("button", { name: "Register" }).closest("form"));
    const renderPage = () =>
        renderWithProviders(<Register />, { route: "/register", path: "/register", routes: <Route path="/" element={<p>home page</p>} /> });

    it("sikeres regisztráció után belép és a főoldalra visz", async () => {
        const api = mockApi({ "POST /api/Auth/register": { token: "jwt.reg.token", user: { id: 3, name: "Neo" } } });
        const user = userEvent.setup();
        renderPage();

        await fill(user);
        await user.click(screen.getByRole("button", { name: "Register" }));
        expect(await screen.findByText("home page")).toBeInTheDocument();
        expect(localStorage.getItem("jwt")).toBe("jwt.reg.token");
        expect(api.calls[0].body).toEqual({ name: "Neo", email: "neo@zion.io", password: "Test123!", confirmPassword: "Test123!" });
    });

    it("kliensoldali validáció", async () => {
        mockApi({});
        const user = userEvent.setup();
        renderPage();

        submit();
        expect(await screen.findByText("All fields are required")).toBeInTheDocument();

        await fill(user, { email: "not-an-email" });
        submit();
        expect(await screen.findByText("Invalid email format")).toBeInTheDocument();
    });

    it("gyenge jelszó és eltérő megerősítés", async () => {
        mockApi({});
        const user = userEvent.setup();
        const first = renderPage();
        await fill(user, { password: "weak" });
        submit();
        expect(await screen.findByText(/Password must be at least 8 characters/)).toBeInTheDocument();
        first.unmount();

        renderPage();
        await fill(user, { confirm: "Other123!" });
        submit();
        expect(await screen.findByText("Passwords do not match")).toBeInTheDocument();
    });

    it("szerverhiba üzenettel és anélkül", async () => {
        const routes = { "POST /api/Auth/register": { status: 400, body: { message: "Email already used" } } };
        mockApi(routes);
        const user = userEvent.setup();
        renderPage();
        await fill(user);
        await user.click(screen.getByRole("button", { name: "Register" }));
        expect(await screen.findByText("Email already used")).toBeInTheDocument();

        routes["POST /api/Auth/register"] = { status: 500, body: {} };
        await user.click(screen.getByRole("button", { name: "Register" }));
        expect(await screen.findByText("Registration failed")).toBeInTheDocument();
    });
});
