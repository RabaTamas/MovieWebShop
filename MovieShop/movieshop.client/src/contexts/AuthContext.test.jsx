import { act, render, screen, waitFor } from "@testing-library/react";

import { AuthProvider, useAuth } from "./AuthContext";
import { makeJwt } from "@/test/utils";

const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

function Probe() {
    const { user, token, loading, login, logout } = useAuth();
    if (loading) return <p>loading</p>;
    return (
        <div>
            <p data-testid="user">{user ? `${user.id}|${user.name}|${user.email}|${user.role}` : "anonymous"}</p>
            <p data-testid="token">{token ?? "none"}</p>
            <button onClick={() => login({ token: "new.token.value", user: { id: 9, name: "trinity" } })}>login</button>
            <button onClick={logout}>logout</button>
        </div>
    );
}

const renderProbe = () =>
    render(
        <AuthProvider>
            <Probe />
        </AuthProvider>
    );

describe("AuthContext", () => {
    it("tárolt token nélkül anonim a felhasználó", async () => {
        renderProbe();
        expect(await screen.findByTestId("user")).toHaveTextContent("anonymous");
    });

    it("érvényes tokenből visszaállítja a felhasználót (rövid claim-nevekkel)", async () => {
        localStorage.setItem("jwt", makeJwt({ nameid: "42", name: "neo", email: "neo@zion.io", role: "Admin" }));

        renderProbe();

        expect(await screen.findByTestId("user")).toHaveTextContent("42|neo|neo@zion.io|Admin");
    });

    it("a .NET hosszú szerepkör-claimjét is felismeri", async () => {
        localStorage.setItem("jwt", makeJwt({ sub: "7", name: "neo", email: "neo@zion.io", [ROLE_CLAIM]: "User" }));

        renderProbe();

        expect(await screen.findByTestId("user")).toHaveTextContent("7|neo|neo@zion.io|User");
    });

    it("lejárt tokent töröl, és nem jelentkeztet be", async () => {
        vi.spyOn(console, "log").mockImplementation(() => {});
        localStorage.setItem("jwt", makeJwt({ nameid: "42", name: "neo" }, { expiresInSeconds: -60 }));

        renderProbe();

        expect(await screen.findByTestId("user")).toHaveTextContent("anonymous");
        expect(localStorage.getItem("jwt")).toBeNull();
    });

    it("hibás formátumú tokent töröl", async () => {
        vi.spyOn(console, "error").mockImplementation(() => {});
        localStorage.setItem("jwt", "not-a-jwt");

        renderProbe();

        expect(await screen.findByTestId("user")).toHaveTextContent("anonymous");
        expect(localStorage.getItem("jwt")).toBeNull();
    });

    it("login elmenti a tokent, logout törli", async () => {
        renderProbe();
        await screen.findByTestId("user");

        act(() => screen.getByText("login").click());
        expect(screen.getByTestId("user")).toHaveTextContent("9|trinity");
        expect(localStorage.getItem("jwt")).toBe("new.token.value");

        act(() => screen.getByText("logout").click());
        await waitFor(() => expect(screen.getByTestId("user")).toHaveTextContent("anonymous"));
        expect(localStorage.getItem("jwt")).toBeNull();
    });
});
