import "@testing-library/jest-dom/vitest";
import { afterEach, vi } from "vitest";
import { cleanup } from "@testing-library/react";

afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.restoreAllMocks();
});

// A jsdom nem implementálja ezeket, a motion és a Radix UI viszont használja
class ObserverStub {
    observe() {}
    unobserve() {}
    disconnect() {}
    takeRecords() {
        return [];
    }
}
globalThis.IntersectionObserver ??= ObserverStub;
globalThis.ResizeObserver ??= ObserverStub;

window.matchMedia ??= (query) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener() {},
    removeEventListener() {},
    addListener() {},
    removeListener() {},
    dispatchEvent: () => false,
});
