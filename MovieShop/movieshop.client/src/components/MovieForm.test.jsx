import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import MovieForm from "./MovieForm";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const extraRoutes = <Route path="/admin/movies" element={<p>movie list</p>} />;
const renderAdd = () => renderWithProviders(<MovieForm />, { route: "/admin/movies/add", path: "/admin/movies/add", routes: extraRoutes });
const renderEdit = () => renderWithProviders(<MovieForm />, { route: "/admin/movies/edit/5", path: "/admin/movies/edit/:id", routes: extraRoutes });

describe("MovieForm", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("új filmet ment kategóriák nélkül", async () => {
        const api = mockApi({ "GET /api/category": [], "POST /api/movie": { id: 9 } });
        const user = userEvent.setup();
        renderAdd();

        expect(screen.getByText("Categories and video can be added after saving.")).toBeInTheDocument();
        expect(screen.getByText("No image URL")).toBeInTheDocument();
        await user.type(screen.getByLabelText("Title *"), "Matrix");
        await user.type(screen.getByLabelText("Description *"), "Neo");
        await user.clear(screen.getByLabelText("Price (Ft) *"));
        await user.type(screen.getByLabelText("Price (Ft) *"), "2500");
        await user.type(screen.getByLabelText("Discounted Price (optional)"), "2000");
        await user.clear(screen.getByLabelText("Discounted Price (optional)"));
        await user.type(screen.getByLabelText("Image URL *"), "https://img/m.jpg");
        expect(screen.getByAltText("Movie preview")).toHaveAttribute("src", "https://img/m.jpg");

        await user.click(screen.getByRole("button", { name: /Save Movie/ }));
        expect(await screen.findByText("movie list")).toBeInTheDocument();
        expect(api.find("POST", "/api/movie")[0].body).toEqual({
            title: "Matrix", description: "Neo", price: 2500, discountedPrice: null, imageUrl: "https://img/m.jpg",
        });
    });

    it("szerkesztésnél betölti és PUT-tal menti a filmet, kategóriákkal", async () => {
        const api = mockApi({
            "GET /api/category": [{ id: 1, name: "Action" }],
            "GET /api/movie/5": { id: 5, title: "Alien", description: "Space", price: 3000, discountedPrice: 2500, imageUrl: "https://i/a.jpg" },
            "PUT /api/movie/5": {},
        });
        const user = userEvent.setup();
        renderEdit();

        expect(await screen.findByDisplayValue("Alien")).toBeInTheDocument();
        expect(screen.getByRole("heading", { name: "Edit Movie" })).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Save Movie/ }));
        expect(await screen.findByText("movie list")).toBeInTheDocument();
        expect(api.find("PUT", "/api/movie/5")[0].body).toMatchObject({ id: 5, title: "Alien", categories: [] });
    });

    it("mentési hiba: szerverüzenet vagy állapotkód", async () => {
        const routes = {
            "GET /api/category": { status: 500 },
            "GET /api/movie/5": { id: 5, title: "Alien", description: "d", price: 1, imageUrl: "https://i/a.jpg", categories: [] },
            "PUT /api/movie/5": { status: 400, body: { message: "Title taken" } },
        };
        mockApi(routes);
        const user = userEvent.setup();
        renderEdit();

        expect(await screen.findByText("Error 500: 500")).toBeInTheDocument();
        await user.click(await screen.findByRole("button", { name: /Save Movie/ }));
        expect(await screen.findByText("Title taken")).toBeInTheDocument();

        routes["PUT /api/movie/5"] = { status: 500, body: {} };
        await user.click(screen.getByRole("button", { name: /Save Movie/ }));
        expect(await screen.findByText("Error 500: 500")).toBeInTheDocument();

        routes["PUT /api/movie/5"] = { status: 502, body: "not json" };
        await user.click(screen.getByRole("button", { name: /Save Movie/ }));
        expect(await screen.findByText("Error 502: 502")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Cancel" }));
        expect(screen.getByText("movie list")).toBeInTheDocument();
    });

    it("nem létező film betöltési hibája; vissza gomb", async () => {
        mockApi({ "GET /api/category": [], "GET /api/movie/5": { status: 404 } });
        const user = userEvent.setup();
        renderEdit();

        expect(await screen.findByText("Error 404: 404")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Back to Movies/ }));
        await waitFor(() => expect(screen.getByText("movie list")).toBeInTheDocument());
    });
});
