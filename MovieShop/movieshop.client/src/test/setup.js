import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach, vi } from "vitest";
import { cleanup } from "@testing-library/react";
import { hls, pwa, signalr, stripe } from "./fakes";

// Böngészőn kívül nem futtatható külső könyvtárak (ld. fakes.jsx)
vi.mock("@microsoft/signalr", async () => (await import("./fakes")).signalrModule);
vi.mock("hls.js", async () => (await import("./fakes")).hlsModule);
vi.mock("@stripe/stripe-js", async () => (await import("./fakes")).stripeJsModule);
vi.mock("@stripe/react-stripe-js", async () => (await import("./fakes")).reactStripeModule);
vi.mock("@react-oauth/google", async () => (await import("./fakes")).googleModule);
vi.mock("sonner", async () => (await import("./fakes")).sonnerModule);

// Az oldalak bőven naplóznak (lejátszó, SignalR); a tesztkimenetet ez ne árassza el
beforeEach(() => {
    vi.spyOn(console, "log").mockImplementation(() => {});
    vi.spyOn(console, "warn").mockImplementation(() => {});
    vi.spyOn(console, "error").mockImplementation(() => {});
});

afterEach(() => {
    cleanup();
    localStorage.clear();
    sessionStorage.clear();
    vi.restoreAllMocks();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
    signalr.reset();
    hls.reset();
    stripe.reset();
    pwa.reset();
});

window.scrollTo = () => {};
Element.prototype.scrollIntoView ??= function () {};
HTMLMediaElement.prototype.play = function () {
    return Promise.resolve();
};
HTMLMediaElement.prototype.pause = function () {};
HTMLMediaElement.prototype.load = function () {};

// A jsdom nem implementálja ezeket, a motion és a Radix UI viszont használja
class ObserverStub {
    observe() {}
    unobserve() {}
    disconnect() {}
    takeRecords() {
        return [];
    }
}
// Minden megfigyelt elemet azonnal láthatónak jelez, így a nézetbe érkezéskor induló
// animációk (motion useInView / whileInView) is lefutnak
class IntersectionObserverStub extends ObserverStub {
    constructor(callback) {
        super();
        this.callback = callback;
    }
    observe(target) {
        queueMicrotask(() =>
            this.callback([{ target, isIntersecting: true, intersectionRatio: 1, boundingClientRect: target.getBoundingClientRect() }], this)
        );
    }
}
globalThis.IntersectionObserver = IntersectionObserverStub;
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
