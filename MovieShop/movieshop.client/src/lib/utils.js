import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Tailwind-osztályok összefűzése ütközéskezeléssel (a shadcn/ui konvenciója):
 * cn("px-2", isActive && "px-4") → "px-4"
 */
export function cn(...inputs) {
    return twMerge(clsx(inputs));
}

/** Ár megjelenítése forintban, ezres tagolással: 1099 → "1 099 Ft" */
export function formatPrice(value) {
    if (value == null) return "";
    return `${Number(value).toLocaleString("hu-HU")} Ft`;
}
