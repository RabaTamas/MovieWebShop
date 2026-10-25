import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import ReviewList from "./ReviewList";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const reviews = [
    { id: 1, userId: 1, userName: "Neo Anderson", content: "Mind-blowing", createdAt: "2030-01-01T10:00:00Z" },
    { id: 2, userId: 2, userName: "trinity", content: "Classic", createdAt: "2030-01-02T10:00:00Z" },
];

const renderList = () =>
    renderWithProviders(<ReviewList movieId={7} />, { routes: <Route path="/login" element={<p>login page</p>} /> });

const reviewCard = (text) => screen.getByText(text).closest(".rounded-xl");

describe("ReviewList", () => {
    it("vendégként megmutatja a véleményeket és belépésre irányít", async () => {
        mockApi({ "GET /api/Review/movie/7": reviews });
        const user = userEvent.setup();
        renderList();

        expect(await screen.findByText("2 reviews")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Show Reviews/ }));
        expect(await screen.findByText("Mind-blowing")).toBeInTheDocument();
        expect(screen.getByText("NA")).toBeInTheDocument();
        expect(screen.queryByRole("button", { name: "Edit review" })).not.toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Sign in" }));
        expect(screen.getByText("login page")).toBeInTheDocument();
    });

    it("üres lista és sikertelen betöltés", async () => {
        mockApi({ "GET /api/Review/movie/7": { status: 500 } });
        const user = userEvent.setup();
        renderList();

        expect(await screen.findByText("No reviews yet", { selector: "p" })).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Show Reviews/ }));
        expect(screen.getByText("Be the first to share your thoughts about this movie!")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Hide Reviews/ }));
        expect(screen.queryByText("Be the first to share your thoughts about this movie!")).not.toBeInTheDocument();
    });

    it("saját véleményt ír, szerkeszt és töröl", async () => {
        loginAs({ id: 1, name: "Neo Anderson" });
        const routes = {
            "GET /api/Review/movie/7": [reviews[1]],
            "POST /api/Review/movie/7": {},
            "PUT /api/Review/1": {},
            "DELETE /api/Review/1": {},
        };
        const api = mockApi(routes);
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderList();

        await user.click(await screen.findByRole("button", { name: /Show Reviews/ }));

        // Írás: megszakítás, majd beküldés
        await user.click(screen.getByRole("button", { name: /Write a Review/ }));
        await user.type(screen.getByPlaceholderText(/Share your thoughts/), "draft");
        await user.click(screen.getByRole("button", { name: "Cancel" }));
        await user.click(screen.getByRole("button", { name: /Write a Review/ }));
        expect(screen.getByPlaceholderText(/Share your thoughts/)).toHaveValue("");
        expect(screen.getByText("Writing a review...")).toBeInTheDocument();
        await user.type(screen.getByPlaceholderText(/Share your thoughts/), "Mind-blowing");
        routes["GET /api/Review/movie/7"] = reviews;
        await user.click(screen.getByRole("button", { name: /Submit Review/ }));

        expect(await screen.findByText("Mind-blowing")).toBeInTheDocument();
        expect(api.find("POST", "/api/Review/movie/7")[0].body).toEqual({ content: "Mind-blowing" });

        // Csak a saját véleménynél van szerkesztés/törlés
        const mine = reviewCard("Mind-blowing");
        expect(within(reviewCard("Classic")).queryByRole("button", { name: "Edit review" })).not.toBeInTheDocument();

        await user.click(within(mine).getByRole("button", { name: "Edit review" }));
        await user.click(screen.getByRole("button", { name: "Cancel" }));
        await user.click(within(reviewCard("Mind-blowing")).getByRole("button", { name: "Edit review" }));
        const editor = screen.getByPlaceholderText("Update your review...");
        await user.clear(editor);
        await user.type(editor, "Still great");
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        await waitFor(() => expect(api.find("PUT", "/api/Review/1")).toHaveLength(1));
        expect(api.find("PUT", "/api/Review/1")[0].body).toEqual({ content: "Still great" });

        await user.click(within(reviewCard("Mind-blowing")).getByRole("button", { name: "Delete review" }));
        await waitFor(() => expect(api.find("DELETE", "/api/Review/1")).toHaveLength(1));
    });

    it("a szerver hibaüzeneteit toastként jeleníti meg", async () => {
        loginAs({ id: 1 });
        const routes = {
            "GET /api/Review/movie/7": reviews,
            "POST /api/Review/movie/7": { status: 400, body: "You already reviewed this" },
            "PUT /api/Review/1": { status: 400, body: "" },
            "DELETE /api/Review/1": { status: 403, body: "" },
        };
        mockApi(routes);
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
        const user = userEvent.setup();
        renderList();
        await user.click(await screen.findByRole("button", { name: /Show Reviews/ }));
        await screen.findByText("Mind-blowing");

        await user.click(screen.getByRole("button", { name: /Write a Review/ }));
        await user.type(screen.getByPlaceholderText(/Share your thoughts/), "again");
        await user.click(screen.getByRole("button", { name: /Submit Review/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("You already reviewed this"));
        routes["POST /api/Review/movie/7"] = new Error("down");
        await user.click(screen.getByRole("button", { name: /Submit Review/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Something went wrong while adding review."));
        await user.click(screen.getByRole("button", { name: "Cancel" }));

        await user.click(within(reviewCard("Mind-blowing")).getByRole("button", { name: "Edit review" }));
        // Üres szöveggel nem küld
        await user.clear(screen.getByPlaceholderText("Update your review..."));
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        await user.type(screen.getByPlaceholderText("Update your review..."), "x");
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to update review"));
        routes["PUT /api/Review/1"] = new Error("down");
        await user.click(screen.getByRole("button", { name: /Save Changes/ }));
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Something went wrong while updating review."));
        await user.click(screen.getByRole("button", { name: "Cancel" }));

        // A megerősítés elutasítása után nem töröl
        const deleteButton = within(reviewCard("Mind-blowing")).getByRole("button", { name: "Delete review" });
        await user.click(deleteButton);
        expect(toast.error).not.toHaveBeenCalledWith("Failed to delete review");
        confirm.mockReturnValue(true);
        await user.click(deleteButton);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Failed to delete review"));
        routes["DELETE /api/Review/1"] = new Error("down");
        await user.click(deleteButton);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Something went wrong while deleting review."));
    });

    it("azonosító hiányában a felhasználónév alapján ismeri fel a saját véleményt", async () => {
        localStorage.setItem("jwt", btoa("{}") + "." + btoa(JSON.stringify({ exp: 9999999999, name: "trinity" })) + ".sig");
        mockApi({ "GET /api/Review/movie/7": [{ id: 5, userName: "trinity", content: "Mine", createdAt: "2030-01-01" }, { id: 6, userName: null, content: "Anon", createdAt: "2030-01-01" }] });
        const user = userEvent.setup();
        renderList();
        await user.click(await screen.findByRole("button", { name: /Show Reviews/ }));

        expect(within(reviewCard("Mine")).getByRole("button", { name: "Edit review" })).toBeInTheDocument();
        expect(within(reviewCard("Anon")).getByText("U")).toBeInTheDocument();
    });
});
