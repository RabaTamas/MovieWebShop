import { cn, formatPrice } from "./utils";

describe("formatPrice", () => {
    it("forintban, ötjegyűtől ezres tagolással formáz", () => {
        // A magyar szabály szerint a négyjegyű számok tagolatlanok
        expect(formatPrice(999)).toBe("999 Ft");
        expect(formatPrice(1099)).toBe("1099 Ft");
        expect(formatPrice(125000)).toMatch(/^125\s000 Ft$/);
    });

    it("számként értelmezi a szöveges értéket", () => {
        expect(formatPrice("25000")).toMatch(/^25\s000 Ft$/);
    });

    it("hiányzó értékre üres szöveget ad", () => {
        expect(formatPrice(null)).toBe("");
        expect(formatPrice(undefined)).toBe("");
    });

    it("a nulla érvényes ár", () => {
        expect(formatPrice(0)).toBe("0 Ft");
    });
});

describe("cn", () => {
    it("az ütköző Tailwind-osztályok közül az utolsót tartja meg", () => {
        expect(cn("px-2 py-1", "px-4")).toBe("py-1 px-4");
    });

    it("kihagyja a hamis értékeket", () => {
        const isActive = false;
        expect(cn("a", isActive && "b", null, undefined, "c")).toBe("a c");
    });
});
