import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import MovieCategories from "./MovieCategories";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const movie = { id: 5, title: "Matrix", price: 2000, discountedPrice: 1500, imageUrl: "/m.jpg", categories: [{ id: 1, name: "Action" }] };
const categories = [
    { id: 1, name: "Action" },
    { id: 2, name: "Sci-Fi" },
];

const renderPage = () =>
    renderWithProviders(<MovieCategories />, {
        route: "/admin/movies/categories/5",
        path: "/admin/movies/categories/:id",
        routes: <Route path="/admin/movies" element={<p>movie list</p>} />,
    });

const categoryButton = (name) => screen.getByRole("button", { name });

describe("MovieCategories", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("kategóriát rendel hozzá és vesz el", async () => {
        const routes = {
            "GET /api/movie/5": movie,
            "GET /api/category": categories,
            "POST /api/movie/5/category/2": {},
            "DELETE /api/movie/5/category/1": {},
        };
        const api = mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("Manage Categories for “Matrix”")).toBeInTheDocument();
        expect(screen.getByText(/1\s?500 Ft/)).toBeInTheDocument();

        await user.click(categoryButton("Sci-Fi"));
        expect(await screen.findByText("Category assignment updated successfully!")).toBeInTheDocument();
        expect(api.find("POST", "/api/movie/5/category/2")).toHaveLength(1);

        await user.click(categoryButton("Action"));
        await waitFor(() => expect(api.find("DELETE", "/api/movie/5/category/1")).toHaveLength(1));
        routes["DELETE /api/movie/5/category/2"] = {};
        await user.click(categoryButton("Sci-Fi"));
        await waitFor(() => expect(screen.getByText("No categories assigned")).toBeInTheDocument());

        routes["POST /api/movie/5/category/1"] = { status: 500 };
        await user.click(categoryButton("Action"));
        expect(await screen.findByText("Error 500: 500")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Back to Movies/ }));
        expect(screen.getByText("movie list")).toBeInTheDocument();
    });

    it("a film vagy a kategóriák betöltési hibája", async () => {
        const routes = { "GET /api/movie/5": { status: 404 } };
        mockApi(routes);
        const first = renderPage();
        expect(await screen.findByText("Error: Error 404: 404")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/movie/5"] = { ...movie, discountedPrice: null };
        routes["GET /api/category"] = { status: 500 };
        renderPage();
        expect(await screen.findByText("Error 500: 500")).toBeInTheDocument();
    });
});
