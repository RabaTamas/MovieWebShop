import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AdminMovies from "./AdminMovies";
import { toast } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const active = [
    { id: 1, title: "Matrix", price: 2000, discountedPrice: 1500, imageUrl: "/m.jpg" },
    { id: 2, title: "Ghost", price: 1000, isDeleted: true },
];
const deleted = [{ id: 3, title: "Alien", price: 3000, isDeleted: true }];

const row = (title) => screen.getByText(title, { selector: "td" }).closest("tr");

describe("AdminMovies oldal", () => {
    beforeEach(() => loginAs({ role: "Admin" }));

    it("nézetek között vált, töröl és visszaállít", async () => {
        const api = mockApi({
            "GET /api/movie": active,
            "GET /api/movie/admin/deleted": deleted,
            "GET /api/movie/admin/all": [...active, ...deleted],
            "DELETE /api/movie/1": {},
            "PATCH /api/movie/3/restore": {},
        });
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminMovies />);

        // Az aktív nézet kiszűri a törölt filmeket
        expect(await screen.findByText("Matrix", { selector: "td" })).toBeInTheDocument();
        expect(screen.queryByText("Ghost", { selector: "td" })).not.toBeInTheDocument();
        expect(within(row("Matrix")).getByRole("link", { name: /Video/ })).toHaveAttribute("href", "/admin/movies/1/video");

        const img = within(row("Matrix")).getByRole("img");
        fireEvent.error(img);
        expect(img.style.visibility).toBe("hidden");

        confirm.mockReturnValueOnce(false);
        await user.click(within(row("Matrix")).getByRole("button", { name: /Delete/ }));
        expect(api.find("DELETE", "/api/movie/1")).toHaveLength(0);
        await user.click(within(row("Matrix")).getByRole("button", { name: /Delete/ }));
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/^Movie deleted successfully/)));
        expect(await screen.findByText("No active movies found.")).toBeInTheDocument();

        await user.click(screen.getByRole("tab", { name: "All Movies" }));
        expect(await screen.findByText("Status")).toBeInTheDocument();
        expect(within(row("Ghost")).getByText("Deleted")).toBeInTheDocument();
        expect(within(row("Matrix")).getByText("Active")).toBeInTheDocument();
        expect(within(row("Ghost")).getByRole("button", { name: /Restore/ })).toBeInTheDocument();

        await user.click(screen.getByRole("tab", { name: "Deleted Movies" }));
        await user.click(await within(await screen.findByRole("row", { name: /Alien/ })).findByRole("button", { name: /Restore/ }));
        await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Movie restored successfully!"));
        expect(await screen.findByText("No deleted movies found.")).toBeInTheDocument();
    });

    it("API-hibák: 401, 403, 404 toast; egyéb hiba hibaoldal", async () => {
        const routes = {
            "GET /api/movie": active,
            "GET /api/movie/admin/deleted": deleted,
            "DELETE /api/movie/1": { status: 401 },
            "PATCH /api/movie/3/restore": { status: 403 },
        };
        mockApi(routes);
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminMovies />);

        const deleteButton = await screen.findByRole("button", { name: /Delete/ });
        await user.click(deleteButton);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Unauthorized. Please check your admin permissions."));
        routes["DELETE /api/movie/1"] = { status: 404 };
        await user.click(deleteButton);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Movie was not found on server. Refreshing view..."));

        await user.click(screen.getByRole("tab", { name: "Deleted Movies" }));
        const restore = await screen.findByRole("button", { name: /Restore/ });
        await user.click(restore);
        await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Forbidden. You don't have permission to restore movies."));

        // Egyéb szerverhiba: a hibaoldal jelenik meg (korábban kezeletlen promise-elutasítás volt)
        routes["PATCH /api/movie/3/restore"] = { status: 500, body: "boom" };
        await user.click(restore);
        expect(await screen.findByText("Error: Failed to restore movie: Error 500: 500 - boom")).toBeInTheDocument();
    });

    it("törlésnél hálózati és szerverhiba", async () => {
        const routes = { "GET /api/movie": active, "DELETE /api/movie/1": new Error("offline") };
        mockApi(routes);
        vi.spyOn(window, "confirm").mockReturnValue(true);
        const user = userEvent.setup();
        renderWithProviders(<AdminMovies />);

        await user.click(await screen.findByRole("button", { name: /Delete/ }));
        expect(await screen.findByText("Error: Failed to delete movie: offline")).toBeInTheDocument();

        const reload = vi.fn();
        vi.stubGlobal("location", { ...window.location, reload });
        await user.click(screen.getByRole("button", { name: /Refresh Page/ }));
        expect(reload).toHaveBeenCalled();
    });

    it("betöltési hibák: JSON és nem JSON válasz, hiányzó token", async () => {
        const routes = { "GET /api/movie": { status: 500 } };
        mockApi(routes);
        const first = renderWithProviders(<AdminMovies />);
        expect(await screen.findByText("Error: Error 500: 500")).toBeInTheDocument();
        first.unmount();

        routes["GET /api/movie"] = { status: 502, body: "<html>", contentType: "text/html" };
        const second = renderWithProviders(<AdminMovies />);
        expect(await screen.findByText(/Server returned 502: 502 \(not JSON\)/)).toBeInTheDocument();
        second.unmount();

        localStorage.clear();
        renderWithProviders(<AdminMovies />);
        expect(await screen.findByText("Error: Authentication token is missing")).toBeInTheDocument();
    });

    it("üres „összes film” nézet", async () => {
        mockApi({ "GET /api/movie": [], "GET /api/movie/admin/all": [] });
        const user = userEvent.setup();
        renderWithProviders(<AdminMovies />);
        expect(await screen.findByRole("link", { name: "Add your first movie" })).toBeInTheDocument();
        await user.click(screen.getByRole("tab", { name: "All Movies" }));
        expect(await screen.findByText("No movies found.")).toBeInTheDocument();
    });
});
