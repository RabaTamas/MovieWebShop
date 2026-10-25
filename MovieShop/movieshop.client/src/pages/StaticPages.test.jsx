import { screen } from "@testing-library/react";

import About from "./About";
import NotFound from "./NotFound";
import Unauthorized from "./Unauthorized";
import Footer from "@/components/Footer";
import { renderWithProviders } from "@/test/utils";

describe("Statikus oldalak", () => {
    it("About: bemutatkozás, funkciók, technológiák és kapcsolat", () => {
        renderWithProviders(<About />);
        expect(screen.getByText("Vast Collection")).toBeInTheDocument();
        expect(screen.getByText("Tailwind CSS + shadcn/ui")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /support@movieshop.com|Contact/i })).toHaveAttribute("href", "mailto:support@movieshop.com");
    });

    it("NotFound: visszavezet a főoldalra", () => {
        renderWithProviders(<NotFound />);
        expect(screen.getAllByRole("link").some((a) => a.getAttribute("href") === "/")).toBe(true);
    });

    it("Unauthorized: főoldal és belépés", () => {
        renderWithProviders(<Unauthorized />);
        expect(screen.getByRole("link", { name: /Go Home/ })).toHaveAttribute("href", "/");
        expect(screen.getByRole("link", { name: /Login/ })).toHaveAttribute("href", "/login");
    });

    it("Footer: navigációs linkek", () => {
        renderWithProviders(<Footer />);
        expect(screen.getByRole("link", { name: "Auctions" })).toHaveAttribute("href", "/auctions");
    });
});
