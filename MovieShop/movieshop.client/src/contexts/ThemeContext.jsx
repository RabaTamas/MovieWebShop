import { createContext, useContext, useEffect, useState } from "react";

const ThemeContext = createContext({ theme: "dark", toggleTheme: () => {} });

const STORAGE_KEY = "movieshop-theme";

function readStoredTheme() {
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        return stored === "light" || stored === "dark" ? stored : "dark";
    } catch {
        return "dark";
    }
}

/**
 * Sötét (alapértelmezett) és világos téma közötti váltás. A választás a
 * böngészőben megmarad; a `dark` osztály a <html> elemre kerül, amire a
 * Tailwind `dark:` variánsa és a shadcn/ui CSS-változói épülnek.
 */
export function ThemeProvider({ children }) {
    const [theme, setTheme] = useState(readStoredTheme);

    useEffect(() => {
        document.documentElement.classList.toggle("dark", theme === "dark");
        // A mobil böngésző / telepített PWA státuszsávjának színe kövesse a témát
        document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#0f0f14" : "#fbfaf8");
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch {
            /* privát mód: a választás csak a munkamenetre szól */
        }
    }, [theme]);

    const toggleTheme = () => setTheme((t) => (t === "dark" ? "light" : "dark"));

    return (
        <ThemeContext.Provider value={{ theme, toggleTheme }}>
            {children}
        </ThemeContext.Provider>
    );
}

export const useTheme = () => useContext(ThemeContext);
