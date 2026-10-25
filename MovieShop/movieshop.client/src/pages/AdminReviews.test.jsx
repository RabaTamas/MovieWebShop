import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminReviews from "./AdminReviews";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const reviews = [
    { id: 1, movie: { id: 7, title: "Matrix" }, user: { name: "neo" }, content: "x".repeat(120), createdAt: "2030-01-01" },
    { id: 2, movie: null, user: null, content: "Short", createdAt: "2030-01-02" },
];

describe("AdminReviews oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("listázza és törli a véleményeket", async () => {
        const routes = { "GET /api/review": reviews, "DELETE /api/review/2": {} };
        const api = mockApi(routes);
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminReviews />);

        expect(await screen.findByRole("link", { name: "Matrix" })).toHaveAttribute("href", "/movies/7");
        expect(screen.getByText("x".repeat(100) + "...")).toBeInTheDocument();
        expect(screen.getByText("Movie not found")).toBeInTheDocument();
        expect(screen.getByText("Unknown User")).toBeInTheDocument();

        const [, second] = screen.getAllByRole("button", { name: /Delete/ });
        confirm.mockReturnValueOnce(false);
        await user.click(second);
        expect(api.find("DELETE", "/api/review/2")).toHaveLength(0);
        await user.click(second);
        await waitFor(() => expect(screen.queryByText("Short")).not.toBeInTheDocument());

        await user.click(screen.getByRole("button", { name: /Delete/ }));
        expect(await screen.findByText("Error: Error 404: 404")).toBeInTheDocument();
    });

    it("üres lista, betöltési hiba, hiányzó token", async () => {
        const routes = { "GET /api/review": [] };
        mockApi(routes);
        const first = renderWithProviders(<AdminReviews />);
        expect(await screen.findByText("No reviews found")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/review"] = { status: 500 };
        const second = renderWithProviders(<AdminReviews />);
        expect(await screen.findByText("Error: Error 500: 500")).toBeInTheDocument();
        second.unmount();

        localStorage.clear();
        renderWithProviders(<AdminReviews />);
        expect(await screen.findByText("Error: Authentication token is missing")).toBeInTheDocument();
    });
});
