import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import Chatbot from "./Chatbot";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

// A Web Speech API-t a komponens betöltéskor ellenőrzi, ezért az importok előtt kell létrehozni
const speech = vi.hoisted(() => {
    const state = { recognitions: [], spoken: [], cancel: 0 };
    class FakeRecognition {
        constructor() {
            this.start = () => this.onstart?.();
            this.stop = () => this.onend?.();
            this.abort = () => {};
            state.recognitions.push(this);
        }
    }
    window.SpeechRecognition = FakeRecognition;
    window.speechSynthesis = {
        speak: (u) => state.spoken.push(u),
        cancel: () => state.cancel++,
    };
    window.SpeechSynthesisUtterance = class {
        constructor(text) {
            this.text = text;
        }
    };
    return state;
});

const open = async (user) => {
    renderWithProviders(<Chatbot />);
    await user.click(screen.getByRole("button", { name: "Open chat" }));
};

const input = () => screen.getByPlaceholderText(/Ask a question|Listening/);

describe("Chatbot", () => {
    beforeEach(() => {
        speech.recognitions.length = 0;
        speech.spoken.length = 0;
    });

    it("GYIK-kérdésre válaszol, és munkamenet-azonosítót küld", async () => {
        const api = mockApi({ "POST /api/Chat/ask": { answer: "We accept cards.", source: "FAQ" } });
        const user = userEvent.setup();
        await open(user);

        await user.click(screen.getByRole("button", { name: "What payment methods do you accept?" }));
        expect(await screen.findByText("We accept cards.")).toBeInTheDocument();
        expect(screen.getByText("FAQ", { selector: "span" })).toBeInTheDocument();

        const [req] = api.find("POST", "/api/Chat/ask");
        expect(req.body.question).toBe("What payment methods do you accept?");
        expect(req.body.sessionId).toMatch(/^session_/);
        expect(req.headers.Authorization).toBeUndefined();
        expect(localStorage.getItem("chatSessionId")).toBe(req.body.sessionId);
        // Vendégnek nincs „Actions" fül
        expect(screen.queryByRole("button", { name: /Actions/ })).not.toBeInTheDocument();
    });

    it("Enterrel és gombbal küld; hiba esetén bocsánatkérő üzenet", async () => {
        mockApi({ "POST /api/Chat/ask": { status: 500 } });
        const user = userEvent.setup();
        await open(user);

        // Üres szöveg nem megy ki
        fireEvent.keyDown(input(), { key: "Enter" });
        await user.type(input(), "hello{Enter}");
        expect(await screen.findByText(/An error occurred/)).toBeInTheDocument();
        expect(screen.getByText("hello")).toBeInTheDocument();

        await user.type(input(), "again");
        await user.click(screen.getByRole("button", { name: "Send message" }));
        await waitFor(() => expect(screen.getAllByText(/An error occurred/)).toHaveLength(2));
    });

    it("agent-válasz: navigál útvonalra és oldalnévre, kosárgombot mutat", async () => {
        loginAs();
        const answers = [
            { answer: "Opening.", source: "agent", action: { type: "navigate", payload: { path: "/my-movies/5/watch" } } },
            { answer: "To orders.", source: "agent", action: { type: "navigate", payload: { page: "orders" } } },
            { answer: "Somewhere.", source: "llm", action: { type: "navigate", payload: { page: "unknown" } } },
            { answer: "Added.", source: "agent", action: { type: "cart_updated" } },
        ];
        const api = mockApi({ "POST /api/Chat/ask": () => answers.shift() });
        const user = userEvent.setup();
        await open(user);

        await user.click(screen.getByRole("button", { name: /Actions/ }));
        await user.click(screen.getByRole("button", { name: /Go to my movies/ }));
        expect(await screen.findByText("Opening.")).toBeInTheDocument();
        expect(screen.getByText("Navigating...")).toBeInTheDocument();
        await waitFor(() => expect(screen.getByTestId("location")).toHaveTextContent("/my-movies/5/watch"), { timeout: 2000 });
        expect(api.calls[0].headers.Authorization).toMatch(/^Bearer /);

        await user.click(screen.getByRole("button", { name: /Go to orders/ }));
        await waitFor(() => expect(screen.getByTestId("location")).toHaveTextContent("/orders"), { timeout: 2000 });

        await user.click(screen.getByRole("button", { name: /Go to home/ }));
        expect(await screen.findByText("AI")).toBeInTheDocument();
        await waitFor(() => expect(screen.getByTestId("location")).toHaveTextContent(/^\/$/), { timeout: 2000 });

        await user.click(screen.getByRole("button", { name: /Go to cart/ }));
        await user.click(await screen.findByRole("button", { name: /View Cart/ }));
        expect(screen.getByTestId("location")).toHaveTextContent("/cart");
    });

    it("kitöltős akciógomb a beviteli mezőbe írja a sablont", async () => {
        loginAs();
        mockApi({});
        const user = userEvent.setup();
        await open(user);

        await user.click(screen.getByRole("button", { name: /Actions/ }));
        await user.click(screen.getByRole("button", { name: /Add movie to cart/ }));
        expect(input()).toHaveValue("Add  to my cart");
        await waitFor(() => expect(input()).toHaveFocus());
        expect(input().selectionStart).toBe(4);

        await user.click(screen.getByRole("button", { name: /Search movie/ }));
        expect(input()).toHaveValue("Search for ");

        await user.click(screen.getByRole("button", { name: /FAQ/ }));
        expect(screen.getByRole("button", { name: "Can I get a refund?" })).toBeInTheDocument();
    });

    it("hangbevitel: felismert szöveget elküld; leállítható", async () => {
        const api = mockApi({ "POST /api/Chat/ask": { answer: "Szia!" } });
        const user = userEvent.setup();
        await open(user);

        await user.click(screen.getByTitle("Switch to Hungarian"));
        expect(screen.getByText("HU")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Start voice input" }));
        expect(screen.getByText("Listening... speak now")).toBeInTheDocument();
        const rec = speech.recognitions[0];
        expect(rec.lang).toBe("hu-HU");

        act(() => rec.onresult({ results: [[{ transcript: "hello voice" }]] }));
        expect(await screen.findByText("Szia!")).toBeInTheDocument();
        expect(api.calls[0].body.question).toBe("hello voice");

        await user.click(screen.getByRole("button", { name: "Start voice input" }));
        await user.click(screen.getByRole("button", { name: "Stop listening" }));
        expect(screen.queryByText("Listening... speak now")).not.toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: "Start voice input" }));
        act(() => speech.recognitions[2].onerror());
        expect(screen.queryByText("Listening... speak now")).not.toBeInTheDocument();
    });

    it("felolvasás bekapcsolva kimondja a választ; bezáráskor leáll", async () => {
        mockApi({ "POST /api/Chat/ask": { answer: "Spoken answer" } });
        const user = userEvent.setup();
        await open(user);

        await user.click(screen.getByRole("button", { name: "Enable voice" }));
        await user.type(input(), "talk{Enter}");
        await screen.findByText("Spoken answer");
        expect(speech.spoken.map((u) => u.text)).toEqual(["Spoken answer"]);
        expect(speech.spoken[0].lang).toBe("en-US");

        const cancelsBefore = speech.cancel;
        await user.click(screen.getByRole("button", { name: "Disable voice" }));
        expect(speech.cancel).toBeGreaterThan(cancelsBefore);

        await user.click(screen.getByRole("button", { name: "Close chat" }));
        expect(screen.getByRole("button", { name: "Open chat" })).toBeInTheDocument();
    });

    it("külső 'chatbot:open' eseményre kinyílik és elküldi az üzenetet", async () => {
        loginAs();
        const api = mockApi({ "POST /api/Chat/ask": { answer: "About Matrix" } });
        renderWithProviders(<Chatbot />);

        act(() => {
            window.dispatchEvent(new CustomEvent("chatbot:open", { detail: { message: "Tell me about Matrix" } }));
        });
        expect(await screen.findByText("About Matrix", {}, { timeout: 2000 })).toBeInTheDocument();
        // A bejelentkezett felhasználó tokenjével megy ki (régi hiba: null token)
        expect(api.calls[0].headers.Authorization).toMatch(/^Bearer /);

        act(() => window.dispatchEvent(new CustomEvent("chatbot:open")));
        expect(api.calls).toHaveLength(1);
    });
});
