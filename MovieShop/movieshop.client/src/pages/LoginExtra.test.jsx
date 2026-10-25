import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import Login from "./Login";
import { mockApi, renderWithProviders } from "@/test/utils";

// A Login.test.jsx a Google-gombot üresre cseréli; itt a globális helyettesítő (fakes.jsx) fut,
// amely gombnyomásra sikeres vagy sikertelen Google-bejelentkezést szimulál.
const renderLogin = () =>
    renderWithProviders(<Login />, {
        route: "/login",
        path: "/login",
        routes: <Route path="/" element={<p>home page</p>} />,
    });

describe("Login: Google-bejelentkezés és hibaágak", () => {
    it("sikeres Google-bejelentkezés a kapott ID-tokennel", async () => {
        const api = mockApi({ "POST /api/Auth/google-login": { token: "jwt.google.token", user: { id: 1 } } });
        const user = userEvent.setup();
        renderLogin();

        await user.click(screen.getByRole("button", { name: "Google sign-in" }));
        expect(await screen.findByText("home page")).toBeInTheDocument();
        expect(localStorage.getItem("jwt")).toBe("jwt.google.token");
        expect(api.calls[0].body).toEqual(expect.objectContaining({ idToken: "google-id-token" }));
    });

    it("Google-hibák: szerverüzenet, alapértelmezett szöveg, kliensoldali hiba", async () => {
        const routes = { "POST /api/Auth/google-login": { status: 401, body: { message: "Google account not allowed" } } };
        mockApi(routes);
        const user = userEvent.setup();
        renderLogin();

        await user.click(screen.getByRole("button", { name: "Google sign-in" }));
        expect(await screen.findByText("Google account not allowed")).toBeInTheDocument();
        routes["POST /api/Auth/google-login"] = { status: 500, body: {} };
        await user.click(screen.getByRole("button", { name: "Google sign-in" }));
        expect(await screen.findByText("Google login failed")).toBeInTheDocument();
        routes["POST /api/Auth/google-login"] = new Error("");
        await user.click(screen.getByRole("button", { name: "Google sign-in" }));
        expect(await screen.findByText("An error occurred during Google login")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Google error" }));
        expect(screen.getByText("Google login failed. Please try again.")).toBeInTheDocument();
    });

    it("hiányzó adatok, alapértelmezett hibaüzenetek a jelszavas és a 2FA-lépésben", async () => {
        const routes = { "POST /api/Auth/login": { status: 500, body: {} } };
        mockApi(routes);
        const user = userEvent.setup();
        renderLogin();

        fireEvent.submit(screen.getByRole("button", { name: "Login" }).closest("form"));
        expect(await screen.findByText("Please enter both email and password")).toBeInTheDocument();

        await user.type(screen.getByLabelText("Email"), "neo@zion.io");
        await user.type(screen.getByLabelText("Password"), "x");
        await user.click(screen.getByRole("button", { name: "Login" }));
        expect(await screen.findByText("Login failed")).toBeInTheDocument();

        routes["POST /api/Auth/login"] = { requiresTwoFactor: true, twoFactorUserId: "7" };
        routes["POST /api/Auth/2fa/login"] = { status: 400, body: {} };
        await user.click(screen.getByRole("button", { name: "Login" }));
        const code = await screen.findByLabelText("Authentication Code");
        fireEvent.submit(code.closest("form"));
        expect(await screen.findByText("Please enter the authentication code")).toBeInTheDocument();
        await user.type(code, "123456");
        await user.click(screen.getByRole("button", { name: "Verify" }));
        expect(await screen.findByText("Invalid code")).toBeInTheDocument();
    });
});
