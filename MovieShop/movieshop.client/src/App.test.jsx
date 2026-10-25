import { render, screen } from "@testing-library/react";

import App from "./App";
import { ThemeProvider } from "@/contexts/ThemeContext";
import { loginAs, mockApi } from "@/test/utils";

const renderAt = (path) => {
    window.history.pushState({}, "", path);
    return render(
        <ThemeProvider>
            <App />
        </ThemeProvider>
    );
};

describe("App útvonalak", () => {
    beforeEach(() => mockApi({ "GET /api/Movie": [], "GET /api/Category": [] }));
    afterEach(() => window.history.pushState({}, "", "/"));

    it("a főoldal a navigációval, lábléccel és chatbottal jelenik meg", async () => {
        renderAt("/");
        expect(await screen.findByRole("heading", { name: "All movies" })).toBeInTheDocument();
        expect(screen.getByRole("button", { name: "Open chat" })).toBeInTheDocument();
        expect(screen.getByRole("contentinfo")).toBeInTheDocument();
    });

    it("ismeretlen útvonalon a 404-es oldal", async () => {
        renderAt("/no-such-page");
        expect((await screen.findAllByText(/404|not found/i)).length).toBeGreaterThan(0);
    });

    it("védett oldal vendégként a belépésre irányít", async () => {
        renderAt("/cart");
        expect(await screen.findByLabelText("Email")).toBeInTheDocument();
        expect(window.location.pathname).toBe("/login");
    });

    it("admin oldal sima felhasználóként a jogosultsághiány-oldalra irányít", async () => {
        loginAs({ role: "User" });
        renderAt("/admin/movies");
        await screen.findByRole("heading", { level: 1 });
        expect(window.location.pathname).toBe("/unauthorized");
    });
});
