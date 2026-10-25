import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import WatchMovie from "./WatchMovie";
import { hls } from "@/test/fakes";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

const movie = { id: 7, title: "Matrix", description: "Neo wakes up", videoFileName: "matrix.mp4", categories: [{ id: 1, name: "Sci-Fi" }] };
const stream = (extra = {}) => ({ url: "https://blob/matrix.m3u8", expiresAt: "2030-01-01T10:00:00Z", ...extra });

const renderPage = () =>
    renderWithProviders(<WatchMovie />, {
        route: "/my-movies/7/watch",
        path: "/my-movies/:movieId/watch",
        routes: <Route path="/my-movies" element={<p>my movies page</p>} />,
    });

const video = () => document.querySelector("video");

describe("WatchMovie oldal", () => {
    beforeEach(() => loginAs());

    it("HLS-folyamot indít, minőséget vált és kezeli a hibákat", async () => {
        const api = mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/stream": stream({ isHls: true }),
            "GET /api/Movie/7/progress": { progressSeconds: 0 },
            "POST /api/Movie/7/progress": {},
        });
        const user = userEvent.setup();
        const { unmount } = renderPage();

        expect(await screen.findByText("Adaptive Streaming (HLS)")).toBeInTheDocument();
        expect(screen.getByText("HLS Adaptive Streaming")).toBeInTheDocument();
        const player = hls.last();
        expect(player.loadSource).toHaveBeenCalledWith("https://blob/matrix.m3u8");
        expect(player.attachMedia).toHaveBeenCalledWith(video());

        // A szegmenskérések csak a saját API felé kapnak tokent
        const xhr = { setRequestHeader: vi.fn() };
        player.config.xhrSetup(xhr, "https://api/api/Movie/7/hls/seg1.ts");
        player.config.xhrSetup(xhr, "https://cdn/other.ts");
        expect(xhr.setRequestHeader).toHaveBeenCalledTimes(1);

        act(() => player.emit("hlsManifestParsed"));
        await user.click(await screen.findByRole("button", { name: "720p" }));
        expect(player.currentLevel).toBe(1);
        await user.click(screen.getByRole("button", { name: "Auto" }));
        expect(player.currentLevel).toBe(-1);

        player.levels[2].bitrate = 5_000_000;
        act(() => player.emit("hlsLevelSwitched", { level: 2 }));
        act(() => player.emit("hlsError", { fatal: true, type: "networkError" }));
        expect(player.startLoad).toHaveBeenCalled();
        act(() => player.emit("hlsError", { fatal: true, type: "mediaError" }));
        expect(player.recoverMediaError).toHaveBeenCalled();
        act(() => player.emit("hlsError", { fatal: false }));
        act(() => player.emit("hlsError", { fatal: true, type: "otherError" }));
        expect(player.destroy).toHaveBeenCalled();

        // Szünetkor és távozáskor menti a pozíciót
        video().currentTime = 42;
        fireEvent.pause(video());
        await waitFor(() => expect(api.find("POST", "/api/Movie/7/progress")).toHaveLength(1));
        expect(api.find("POST", "/api/Movie/7/progress")[0].body).toEqual({ progressSeconds: 42 });
        unmount();
        expect(api.find("POST", "/api/Movie/7/progress")).toHaveLength(2);
    });

    it("mentett pozíciótól folytat, és elölről is indítható", async () => {
        mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/stream": stream(),
            "GET /api/Movie/7/progress": { progressSeconds: 125 },
        });
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("00:02:05")).toBeInTheDocument();
        expect(screen.getByText("Streaming from Azure Blob Storage")).toBeInTheDocument();
        await waitFor(() => expect(video().src).toBe("https://blob/matrix.m3u8"));

        fireEvent.loadedMetadata(video());
        expect(video().currentTime).toBe(125);

        await user.click(screen.getByRole("button", { name: /Start from beginning/ }));
        expect(video().currentTime).toBe(0);
        expect(screen.queryByText("00:02:05")).not.toBeInTheDocument();
    });

    it("a folytatás-értesítés bezárható; a 30 mp-es mentés lejátszás közben fut", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        try {
            const api = mockApi({
                "GET /api/Movie/7": movie,
                "GET /api/Movie/7/stream": stream({ availableQualities: { "720p": "a", "1080p": "b" } }),
                "GET /api/Movie/7/progress": { progressSeconds: 60 },
                "POST /api/Movie/7/progress": {},
            });
            const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
            renderPage();

            expect(await screen.findByText("Available: 720p, 1080p")).toBeInTheDocument();
            await user.click(await screen.findByRole("button", { name: "Close" }));
            expect(screen.queryByText(/Continuing from/)).not.toBeInTheDocument();

            Object.defineProperty(video(), "paused", { configurable: true, get: () => false });
            video().currentTime = 90;
            await act(async () => vi.advanceTimersByTime(30_000));
            expect(api.find("POST", "/api/Movie/7/progress")).toHaveLength(1);
        } finally {
            vi.useRealTimers();
        }
    });

    it("HLS nélküli böngészőben natív lejátszásra vált (Safari)", async () => {
        hls.supported = false;
        mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/stream": stream({ isHls: true }),
            "GET /api/Movie/7/progress": { status: 500 },
        });
        const canPlay = vi.spyOn(HTMLMediaElement.prototype, "canPlayType").mockReturnValue("maybe");
        renderPage();

        await waitFor(() => expect(video()?.src).toBe("https://blob/matrix.m3u8"));
        fireEvent.loadedMetadata(video());
        expect(canPlay).toHaveBeenCalledWith("application/vnd.apple.mpegurl");
        expect(hls.instances).toHaveLength(0);
    });

    it("HLS egyáltalán nem támogatott: nem indít lejátszót", async () => {
        hls.supported = false;
        mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/stream": stream({ isHls: true }),
            "GET /api/Movie/7/progress": new Error("offline"),
        });
        vi.spyOn(HTMLMediaElement.prototype, "canPlayType").mockReturnValue("");
        renderPage();

        await screen.findByText("Adaptive Streaming (HLS)");
        expect(video().getAttribute("src")).toBeNull();
    });

    it("streaming hiányában a trailert játssza le", async () => {
        mockApi({
            "GET /api/Movie/7": { ...movie, videoFileName: null },
            "GET /api/Movie/7/trailer": { youtubeKey: "abc", name: "Official Trailer" },
        });
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByTitle("Official Trailer")).toHaveAttribute("src", expect.stringContaining("/embed/abc"));
        expect(screen.getByText("Currently Playing")).toBeInTheDocument();
        expect(screen.getByText(/Full movie available after transcoding/)).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /Watch Party/ })).toHaveAttribute("href", "/my-movies/7/watch-party");

        await user.click(screen.getByRole("button", { name: /Back to My Movies/ }));
        expect(screen.getByText("my movies page")).toBeInTheDocument();
    });

    it("hibás streamválasznál a trailerre esik vissza; ha az sincs, jelzi", async () => {
        mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/stream": new Error("network"),
            "GET /api/Movie/7/trailer": {},
        });
        renderPage();

        expect(await screen.findByText("No video available for this movie.")).toBeInTheDocument();
    });

    it("meg nem vásárolt filmnél a szerver üzenetét mutatja", async () => {
        mockApi({
            "GET /api/Movie/7": movie,
            "GET /api/Movie/7/stream": { status: 403 },
            "GET /api/Movie/7/trailer": { status: 403, body: { message: "Buy it first" } },
        });
        renderPage();

        expect(await screen.findByText("Buy it first")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: "View Movie Details" })).toHaveAttribute("href", "/movies/7");
    });

    it("403 üzenet nélkül alapértelmezett szöveget, egyéb hibánál a szerver üzenetét mutatja", async () => {
        mockApi({
            "GET /api/Movie/7": { ...movie, videoFileName: null },
            "GET /api/Movie/7/trailer": { status: 403, body: {} },
        });
        renderPage();
        expect(await screen.findByText("You need to purchase this movie to watch it.")).toBeInTheDocument();
    });

    it("trailer-hiba: a szerver üzenete vagy alapértelmezett szöveg", async () => {
        mockApi({
            "GET /api/Movie/7": { ...movie, videoFileName: null },
            "GET /api/Movie/7/trailer": { status: 500, body: {} },
        });
        renderPage();
        expect(await screen.findByText("Failed to load video")).toBeInTheDocument();
    });

    it("nem létező filmnél hibát jelez", async () => {
        mockApi({ "GET /api/Movie/7": { status: 404 } });
        renderPage();
        expect(await screen.findByText("Movie not found")).toBeInTheDocument();
    });
});
