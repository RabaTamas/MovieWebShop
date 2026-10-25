import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import MovieCard from "./MovieCard";
import { ThemeProvider, useTheme } from "@/contexts/ThemeContext";

describe("MovieCard 3D-döntés", () => {
    it("egérmozgásra beállítja a döntés CSS-változóit, elhagyáskor visszaállít; érintést figyelmen kívül hagy", () => {
        render(
            <MemoryRouter>
                <MovieCard movie={{ id: 1, title: "Matrix", price: 1000 }} />
            </MemoryRouter>
        );
        const link = screen.getByRole("link");
        const poster = link.firstChild;
        poster.getBoundingClientRect = () => ({ left: 0, top: 0, width: 100, height: 200 });

        // A jsdom-ban nincs PointerEvent, ezért egéreseményből építjük fel a pointerType-pal
        const move = (pointerType) =>
            fireEvent(link, Object.assign(new MouseEvent("pointermove", { bubbles: true, clientX: 100, clientY: 0 }), { pointerType }));

        move("touch");
        expect(poster.style.getPropertyValue("--ry")).toBe("");

        move("mouse");
        expect(poster.style.getPropertyValue("--ry")).toBe("5deg");
        expect(poster.style.getPropertyValue("--gx")).toBe("100%");

        fireEvent.pointerLeave(link);
        expect(poster.style.getPropertyValue("--ry")).toBe("0deg");
    });
});

describe("ThemeContext", () => {
    const Toggle = () => {
        const { theme, toggleTheme } = useTheme();
        return <button onClick={toggleTheme}>{theme}</button>;
    };

    it("a mentett világos témával indul", () => {
        localStorage.setItem("movieshop-theme", "light");
        render(<ThemeProvider><Toggle /></ThemeProvider>);
        expect(screen.getByRole("button")).toHaveTextContent("light");
    });

    it("tiltott tárhely (privát mód) mellett is működik", async () => {
        vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
            throw new Error("denied");
        });
        vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
            throw new Error("denied");
        });
        const user = userEvent.setup();
        render(<ThemeProvider><Toggle /></ThemeProvider>);
        expect(screen.getByRole("button")).toHaveTextContent("dark");
        await user.click(screen.getByRole("button"));
        expect(screen.getByRole("button")).toHaveTextContent("light");
    });
});
