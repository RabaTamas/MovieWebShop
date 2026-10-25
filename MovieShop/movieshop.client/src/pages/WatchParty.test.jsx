import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import WatchParty from "./WatchParty";
import { hls, signalr } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const movie = { id: 7, title: "Matrix", imageUrl: "/matrix.jpg" };

const renderPage = () =>
    renderWithProviders(<WatchParty />, {
        route: "/my-movies/7/watch-party",
        path: "/my-movies/:movieId/watch-party",
        routes: <Route path="/my-movies" element={<p>my movies page</p>} />,
    });

const video = () => document.querySelector("video");
const invoked = (method) => signalr.last().invocations.filter(([m]) => m === method);

describe("WatchParty oldal", () => {
    beforeEach(() => loginAs({ name: "neo" }));

    it("házigazdaként szobát nyit, szinkronizál és csevegést küld", async () => {
        mockApi({ "GET /api/Movie/7": movie, "GET /api/Movie/7/stream": { url: "https://blob/m.m3u8", isHls: true } });
        signalr.invokeResults.CreateRoom = "ABC123";
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("Matrix")).toBeInTheDocument();
        await user.click(screen.getByRole("button", { name: /Start Watch Party/ }));

        expect(await screen.findByText("ABC123")).toBeInTheDocument();
        expect(screen.getByText("Room created! Share code: ABC123")).toBeInTheDocument();
        expect(screen.getByText("Host")).toBeInTheDocument();
        expect(signalr.last().url).toMatch(/\/hubs\/watchparty$/);
        expect(signalr.last().options.accessTokenFactory()).toBe(localStorage.getItem("jwt"));
        expect(invoked("CreateRoom")[0]).toEqual(["CreateRoom", 7]);

        // HLS-lejátszó a szobában; a token csak a saját API-nak megy
        await waitFor(() => expect(hls.instances).toHaveLength(1));
        const xhr = { setRequestHeader: vi.fn() };
        hls.last().config.xhrSetup(xhr, "https://x/api/Movie/7/hls/a.ts");
        hls.last().config.xhrSetup(xhr, "https://cdn/b.ts");
        expect(xhr.setRequestHeader).toHaveBeenCalledTimes(1);

        // A házigazda lejátszási eseményeit továbbítja
        video().currentTime = 12;
        fireEvent.play(video());
        fireEvent.pause(video());
        fireEvent.seeked(video());
        expect(invoked("Play")[0]).toEqual(["Play", "ABC123", 12]);
        expect(invoked("Pause")).toHaveLength(1);
        expect(invoked("Seek")).toHaveLength(1);

        // Szerverről érkező események
        act(() => signalr.emit("UserJoined", "trinity"));
        expect(screen.getByText("trinity joined the party")).toBeInTheDocument();
        expect(screen.getByText("2 watching")).toBeInTheDocument();
        act(() => signalr.emit("HostChanged", "trinity"));
        expect(screen.getByText("trinity is now the host")).toBeInTheDocument();
        act(() => signalr.emit("UserLeft", "trinity"));
        expect(screen.getByText("trinity left the party")).toBeInTheDocument();
        act(() => signalr.emit("ChatMessage", "morpheus", "hello"));
        expect(screen.getByText("hello")).toBeInTheDocument();

        // Távoli szinkron közben (echo-védelem) nem küld vissza eseményt
        act(() => signalr.emit("Pause", 30));
        fireEvent.pause(video());
        expect(invoked("Pause")).toHaveLength(1);

        await user.type(screen.getByLabelText("Chat message"), "  hi all  ");
        await user.click(screen.getByRole("button", { name: "Send" }));
        expect(invoked("SendMessage")[0]).toEqual(["SendMessage", "ABC123", "hi all"]);
        expect(screen.getByLabelText("Chat message")).toHaveValue("");

        // Üres üzenet nem megy ki; hibás küldésnél a szöveg megmarad
        await user.click(screen.getByRole("button", { name: "Send" }));
        expect(invoked("SendMessage")).toHaveLength(1);
        signalr.invokeResults.SendMessage = new Error("hub down");
        await user.type(screen.getByLabelText("Chat message"), "lost");
        await user.click(screen.getByRole("button", { name: "Send" }));
        expect(screen.getByLabelText("Chat message")).toHaveValue("lost");

        await user.click(screen.getByRole("button", { name: /Copy/ }));
        expect(screen.getByText("Copied!")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Leave/ }));
        expect(await screen.findByRole("button", { name: /Start Watch Party/ })).toBeInTheDocument();
        expect(signalr.connections[0].state).toBe("Disconnected");
    });

    it("vendégként csatlakozik, követi a házigazdát és újraszinkronizál", async () => {
        mockApi({ "GET /api/Movie/7": movie, "GET /api/Movie/7/stream": { url: "https://blob/m.mp4" } });
        const user = userEvent.setup();
        renderPage();
        await screen.findByText("Matrix");

        await user.type(screen.getByLabelText("Room code"), "abc123{Enter}");
        expect(await screen.findByText(/Host controls playback/)).toBeInTheDocument();
        expect(invoked("JoinRoom")[0]).toEqual(["JoinRoom", "ABC123", "neo"]);
        await waitFor(() => expect(video().src).toBe("https://blob/m.mp4"));

        act(() => signalr.emit("RoomState", { members: ["morpheus", "neo"], position: 0 }));
        expect(screen.getByText("Viewers (2)")).toBeInTheDocument();
        act(() => signalr.emit("RoomState", { members: ["morpheus", "neo"], position: 40, isPlaying: false }));
        expect(video().currentTime).toBe(40);
        act(() => signalr.emit("RoomState", { members: ["morpheus", "neo"], position: 50, isPlaying: true, serverTime: Date.now() }));
        expect(video().currentTime).toBeGreaterThanOrEqual(50);

        act(() => signalr.emit("Play", 100, Date.now()));
        expect(video().currentTime).toBeGreaterThanOrEqual(100);
        act(() => signalr.emit("Seek", 200, Date.now()));
        expect(video().currentTime).toBeGreaterThanOrEqual(200);
        act(() => signalr.emit("Pause", 210));
        expect(video().currentTime).toBe(210);

        // A vendég eseményei nem mennek a szerverre
        fireEvent.play(video());
        expect(invoked("Play")).toHaveLength(0);

        signalr.invokeResults.GetRoomState = { position: 300, isPlaying: true, serverTime: Date.now() };
        await user.click(screen.getByRole("button", { name: /Re-sync/ }));
        expect(video().currentTime).toBeGreaterThanOrEqual(300);
        signalr.invokeResults.GetRoomState = { position: 310, isPlaying: false, serverTime: Date.now() };
        await user.click(screen.getByRole("button", { name: /Re-sync/ }));
        expect(video().currentTime).toBe(310);
        signalr.invokeResults.GetRoomState = new Error("gone");
        await user.click(screen.getByRole("button", { name: /Re-sync/ }));
        expect(video().currentTime).toBe(310);
    });

    it("sikertelen csatlakozásnál és szobanyitásnál hibát mutat", async () => {
        mockApi({ "GET /api/Movie/7": movie, "GET /api/Movie/7/stream": { url: "https://blob/m.mp4" } });
        const user = userEvent.setup();
        renderPage();
        await screen.findByText("Matrix");

        // Üres kóddal az Enter nem csinál semmit
        fireEvent.keyDown(screen.getByLabelText("Room code"), { key: "Enter" });
        expect(signalr.connections).toHaveLength(0);

        signalr.invokeResults.JoinRoom = new Error("Room not found: XYZ");
        await user.type(screen.getByLabelText("Room code"), "xyz");
        await user.click(screen.getByRole("button", { name: "Join" }));
        expect(await screen.findByText("Failed to join room: Room not found: XYZ")).toBeInTheDocument();
        expect(signalr.last().state).toBe("Disconnected");

        signalr.invokeResults.JoinRoom = new Error("");
        await user.click(screen.getByRole("button", { name: "Join" }));
        expect(await screen.findByText("Failed to join room: Room not found")).toBeInTheDocument();

        signalr.invokeResults.CreateRoom = new Error("");
        await user.click(screen.getByRole("button", { name: /Start Watch Party/ }));
        expect(await screen.findByText("Failed to create room: Connection error")).toBeInTheDocument();

        signalr.failStart = true;
        await user.click(screen.getByRole("button", { name: /Start Watch Party/ }));
        expect(await screen.findByText("Failed to create room: connection failed")).toBeInTheDocument();
    });

    it("meg nem vásárolt filmnél letiltja a szobanyitást", async () => {
        mockApi({ "GET /api/Movie/7": movie, "GET /api/Movie/7/stream": { status: 403 } });
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("You need to purchase this movie to watch it.")).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Start Watch Party/ })).toBeDisabled();
        await user.click(screen.getByRole("button", { name: /Back to My Movies/ }));
        expect(screen.getByText("my movies page")).toBeInTheDocument();
    });

    it("hálózati hibánál hibaüzenet", async () => {
        mockApi({ "GET /api/Movie/7": new Error("down") });
        renderPage();
        expect(await screen.findByText("Failed to load movie.")).toBeInTheDocument();
    });
});
