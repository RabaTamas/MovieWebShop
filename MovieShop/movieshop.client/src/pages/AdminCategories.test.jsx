import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminCategories from "./AdminCategories";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const categories = [
    { id: 1, name: "Action" },
    { id: 2, name: "Drama" },
];

const row = (name) => screen.getByRole("cell", { name }).closest("tr");

describe("AdminCategories oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("token nélkül hibát jelez", async () => {
        localStorage.clear();
        mockApi({});
        renderWithProviders(<AdminCategories />);
        expect(await screen.findByText("Authentication token is missing")).toBeInTheDocument();
    });

    it("betöltési hiba", async () => {
        mockApi({ "GET /api/category": { status: 500 } });
        renderWithProviders(<AdminCategories />);
        expect(await screen.findByText(/^Error 500/)).toBeInTheDocument();
    });

    it("kategóriát ad hozzá, szerkeszt és töröl", async () => {
        const api = mockApi({
            "GET /api/category": categories,
            "POST /api/category": { id: 3, name: "Sci-Fi" },
            "PUT /api/category/2": {},
            "DELETE /api/category/1": {},
        });
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminCategories />);

        expect(await screen.findByText("All Categories (2)")).toBeInTheDocument();

        await user.type(screen.getByPlaceholderText("Category name"), "  Sci-Fi  ");
        await user.click(screen.getByRole("button", { name: "Add" }));
        expect(await screen.findByText("All Categories (3)")).toBeInTheDocument();
        expect(api.find("POST", "/api/category")[0].body).toEqual({ name: "Sci-Fi" });

        await user.click(within(row("Drama")).getByRole("button", { name: /Edit/ }));
        const editor = screen.getByDisplayValue("Drama");
        await user.clear(editor);
        await user.type(editor, " Thriller ");
        await user.click(screen.getByRole("button", { name: "Save" }));
        expect(await screen.findByRole("cell", { name: "Thriller" })).toBeInTheDocument();
        expect(api.find("PUT", "/api/category/2")[0].body).toEqual({ id: 2, name: "Thriller" });

        // A szerkesztő bezárható
        await user.click(within(row("Thriller")).getByRole("button", { name: /Edit/ }));
        await user.click(within(screen.getByText("Edit Category").closest("[data-slot=card]")).getByRole("button", { name: "Close" }));
        expect(screen.queryByText("Edit Category")).not.toBeInTheDocument();

        confirm.mockReturnValueOnce(false);
        await user.click(within(row("Action")).getByRole("button", { name: /Delete/ }));
        expect(api.find("DELETE", "/api/category/1")).toHaveLength(0);
        await user.click(within(row("Action")).getByRole("button", { name: /Delete/ }));
        await waitFor(() => expect(screen.queryByRole("cell", { name: "Action" })).not.toBeInTheDocument());
    });

    it("műveleti hibákat jelez, a hibaüzenet bezárható", async () => {
        const routes = {
            "GET /api/category": categories,
            "POST /api/category": { status: 409, body: "Duplicate" },
            "PUT /api/category/1": { status: 400, body: "Invalid" },
            "DELETE /api/category/1": { status: 404, body: "" },
            "DELETE /api/category/2": { status: 409, body: "In use" },
        };
        mockApi(routes);
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminCategories />);
        await screen.findByText("All Categories (2)");

        await user.type(screen.getByPlaceholderText("Category name"), "Action");
        await user.click(screen.getByRole("button", { name: "Add" }));
        expect(await screen.findByText("Failed to add category: Error 409: Duplicate")).toBeInTheDocument();

        await user.click(within(row("Action")).getByRole("button", { name: /Edit/ }));
        await user.click(screen.getByRole("button", { name: "Save" }));
        expect(await screen.findByText("Failed to update category: Error 400: Invalid")).toBeInTheDocument();
        await user.click(within(screen.getByText("Edit Category").closest("[data-slot=card]")).getByRole("button", { name: "Close" }));

        await user.click(within(row("Action")).getByRole("button", { name: /Delete/ }));
        expect(await screen.findByText(/Category with ID 1 was not found/)).toBeInTheDocument();
        await user.click(within(row("Drama")).getByRole("button", { name: /Delete/ }));
        expect(await screen.findByText("Failed to delete category: Error 409: In use")).toBeInTheDocument();
        routes["DELETE /api/category/2"] = { status: 500, body: "" };
        await user.click(within(row("Drama")).getByRole("button", { name: /Delete/ }));
        expect(await screen.findByText("Failed to delete category: Error 500: 500")).toBeInTheDocument();

        await user.click(screen.getAllByRole("button", { name: "Close" })[0]);
        expect(screen.queryByText(/Failed to delete category/)).not.toBeInTheDocument();
    });

    it("üres lista", async () => {
        mockApi({ "GET /api/category": [] });
        renderWithProviders(<AdminCategories />);
        expect(await screen.findByText("No categories found")).toBeInTheDocument();
    });
});
