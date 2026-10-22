import { screen } from "@testing-library/react";
import { Route } from "react-router-dom";

import PrivateRoute from "./PrivateRoute";
import AdminRoute from "./AdminRoute";
import { makeJwt, renderWithProviders } from "@/test/utils";

const extraRoutes = (
    <>
        <Route path="/login" element={<p>login page</p>} />
        <Route path="/unauthorized" element={<p>unauthorized page</p>} />
    </>
);

const loginAs = (role) => localStorage.setItem("jwt", makeJwt({ nameid: "1", name: "neo", role }));

describe("PrivateRoute", () => {
    const renderGuarded = () =>
        renderWithProviders(
            <PrivateRoute>
                <p>secret orders</p>
            </PrivateRoute>,
            { route: "/orders", path: "/orders", routes: extraRoutes }
        );

    it("bejelentkezés nélkül a login oldalra irányít", async () => {
        renderGuarded();

        expect(await screen.findByText("login page")).toBeInTheDocument();
        expect(screen.getByTestId("location")).toHaveTextContent("/login");
        expect(screen.queryByText("secret orders")).not.toBeInTheDocument();
    });

    it("bejelentkezett felhasználónak megjeleníti a tartalmat", async () => {
        loginAs("User");
        renderGuarded();

        expect(await screen.findByText("secret orders")).toBeInTheDocument();
    });
});

describe("AdminRoute", () => {
    const renderGuarded = () =>
        renderWithProviders(
            <AdminRoute>
                <p>admin dashboard</p>
            </AdminRoute>,
            { route: "/admin/movies", path: "/admin/movies", routes: extraRoutes }
        );

    it("bejelentkezés nélkül a login oldalra irányít", async () => {
        renderGuarded();
        expect(await screen.findByText("login page")).toBeInTheDocument();
    });

    it("sima felhasználót az unauthorized oldalra irányít", async () => {
        loginAs("User");
        renderGuarded();

        expect(await screen.findByText("unauthorized page")).toBeInTheDocument();
        expect(screen.queryByText("admin dashboard")).not.toBeInTheDocument();
    });

    it("adminnak megjeleníti a tartalmat", async () => {
        loginAs("Admin");
        renderGuarded();

        expect(await screen.findByText("admin dashboard")).toBeInTheDocument();
    });
});
