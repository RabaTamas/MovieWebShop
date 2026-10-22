import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import Login from "./Login";
import { renderWithProviders } from "@/test/utils";

vi.mock("@react-oauth/google", () => ({ GoogleLogin: () => null }));

const jsonResponse = (body, ok = true) => ({ ok, json: async () => body });

const renderLogin = () =>
    renderWithProviders(<Login />, {
        route: "/login",
        path: "/login",
        routes: <Route path="/" element={<p>home page</p>} />,
    });

const submitCredentials = async (user, email = "neo@zion.io", password = "Test123!") => {
    await user.type(screen.getByLabelText("Email"), email);
    await user.type(screen.getByLabelText("Password"), password);
    await user.click(screen.getByRole("button", { name: "Login" }));
};

describe("Login oldal", () => {
    let fetchMock;

    beforeEach(() => {
        fetchMock = vi.fn();
        vi.stubGlobal("fetch", fetchMock);
    });

    afterEach(() => vi.unstubAllGlobals());

    it("sikeres belépés után elmenti a tokent és a főoldalra navigál", async () => {
        fetchMock.mockResolvedValue(jsonResponse({ token: "jwt.value.here", user: { id: 1, name: "neo" } }));
        const user = userEvent.setup();
        renderLogin();

        await submitCredentials(user);

        expect(await screen.findByText("home page")).toBeInTheDocument();
        expect(localStorage.getItem("jwt")).toBe("jwt.value.here");

        const [url, options] = fetchMock.mock.calls[0];
        expect(url).toMatch(/\/api\/Auth\/login$/);
        expect(JSON.parse(options.body)).toEqual({ email: "neo@zion.io", password: "Test123!" });
    });

    it("a szerver hibaüzenetét megjeleníti", async () => {
        fetchMock.mockResolvedValue(jsonResponse({ message: "Invalid email or password" }, false));
        const user = userEvent.setup();
        renderLogin();

        await submitCredentials(user, "neo@zion.io", "wrong");

        expect(await screen.findByText("Invalid email or password")).toBeInTheDocument();
        expect(localStorage.getItem("jwt")).toBeNull();
        expect(screen.getByTestId("location")).toHaveTextContent("/login");
    });

    it("bekapcsolt 2FA esetén a kódot kéri, és csak 6 számjeggyel engedi tovább", async () => {
        fetchMock
            .mockResolvedValueOnce(jsonResponse({ requiresTwoFactor: true, twoFactorUserId: "42" }))
            .mockResolvedValueOnce(jsonResponse({ token: "jwt.after.2fa", user: { id: 42 } }));
        const user = userEvent.setup();
        renderLogin();

        await submitCredentials(user);

        const codeInput = await screen.findByLabelText("Authentication Code");
        const verify = screen.getByRole("button", { name: "Verify" });
        expect(localStorage.getItem("jwt")).toBeNull();

        // A nem számjegy karaktereket kiszűri
        await user.type(codeInput, "12ab34");
        expect(codeInput).toHaveValue("1234");
        expect(verify).toBeDisabled();

        await user.type(codeInput, "56");
        expect(verify).toBeEnabled();
        await user.click(verify);

        expect(await screen.findByText("home page")).toBeInTheDocument();
        expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({ twoFactorUserId: "42", code: "123456" });
        expect(localStorage.getItem("jwt")).toBe("jwt.after.2fa");
    });

    it("a 2FA lépésből vissza lehet lépni a jelszavas űrlapra", async () => {
        fetchMock.mockResolvedValue(jsonResponse({ requiresTwoFactor: true, twoFactorUserId: "42" }));
        const user = userEvent.setup();
        renderLogin();

        await submitCredentials(user);
        await user.click(await screen.findByRole("button", { name: /Back to login/ }));

        expect(screen.getByLabelText("Password")).toBeInTheDocument();
    });
});
