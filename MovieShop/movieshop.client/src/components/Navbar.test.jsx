import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import Navbar from "./Navbar";
import { loginAs, renderWithProviders } from "@/test/utils";

describe("Navbar", () => {
    it("vendégnek belépés/regisztráció; témaváltás", async () => {
        const user = userEvent.setup();
        renderWithProviders(<Navbar />);

        expect(screen.getByRole("link", { name: "Register" })).toHaveAttribute("href", "/register");
        expect(screen.queryByRole("link", { name: "My Cart" })).not.toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Switch to light mode" }));
        expect(document.documentElement).not.toHaveClass("dark");
        await user.click(screen.getByRole("button", { name: "Switch to dark mode" }));
        expect(document.documentElement).toHaveClass("dark");

        // Mobil menü vendégként
        await user.click(screen.getByRole("button", { name: "Toggle navigation" }));
        const sheet = await screen.findByRole("dialog");
        expect(within(sheet).queryByText("Account")).not.toBeInTheDocument();
        await user.click(within(sheet).getByRole("link", { name: "Login" }));
        await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
        expect(screen.getByTestId("location")).toHaveTextContent("/login");
    });

    it("bejelentkezett felhasználó menüje és kijelentkezés", async () => {
        loginAs({ name: "Neo Anderson", email: "neo@zion.io" });
        const user = userEvent.setup();
        renderWithProviders(<Navbar />);

        expect(screen.getByRole("link", { name: "My Cart" })).toHaveAttribute("href", "/cart");
        expect(screen.queryByRole("button", { name: /Admin Panel/ })).not.toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Settings" }));
        const menu = await screen.findByRole("menu");
        expect(within(menu).getByText("neo@zion.io")).toBeInTheDocument();
        await user.click(within(menu).getByRole("menuitem", { name: "Orders" }));
        expect(screen.getByTestId("location")).toHaveTextContent("/orders");

        await user.click(screen.getByRole("button", { name: "Settings" }));
        await user.click(await screen.findByRole("menuitem", { name: "Logout" }));
        expect(await screen.findByRole("link", { name: "Register" })).toBeInTheDocument();
        expect(localStorage.getItem("jwt")).toBeNull();
    });

    it("admin menü asztali és mobil nézetben; mobil kijelentkezés", async () => {
        loginAs({ name: "", email: "admin@movieshop.hu", role: "Admin" });
        const user = userEvent.setup();
        renderWithProviders(<Navbar />);

        expect(screen.getByText("admin")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Admin Panel/ }));
        await user.click(await screen.findByRole("menuitem", { name: "Manage Auctions" }));
        expect(screen.getByTestId("location")).toHaveTextContent("/admin/auctions");

        await user.click(screen.getByRole("button", { name: "Toggle navigation" }));
        let sheet = await screen.findByRole("dialog");
        expect(within(sheet).getByText("Admin Panel")).toBeInTheDocument();
        await user.click(within(sheet).getByRole("link", { name: "Manage Users" }));
        expect(screen.getByTestId("location")).toHaveTextContent("/admin/users");

        await user.click(screen.getByRole("button", { name: "Toggle navigation" }));
        sheet = await screen.findByRole("dialog");
        await user.click(within(sheet).getByRole("link", { name: "My Cart" }));
        await user.click(screen.getByRole("button", { name: "Toggle navigation" }));
        sheet = await screen.findByRole("dialog");
        await user.click(within(sheet).getByRole("link", { name: "Profile" }));
        await user.click(screen.getByRole("button", { name: "Toggle navigation" }));
        sheet = await screen.findByRole("dialog");
        await user.click(within(sheet).getByRole("link", { name: "About" }));
        await user.click(screen.getByRole("button", { name: "Toggle navigation" }));
        sheet = await screen.findByRole("dialog");
        await user.click(within(sheet).getByRole("button", { name: /Logout/ }));
        expect(await screen.findByRole("link", { name: "Register" })).toBeInTheDocument();
    });
});
