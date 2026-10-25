import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route } from "react-router-dom";

import AdminVideoUpload from "./AdminVideoUpload";
import { loginAs, mockApi, renderWithProviders } from "@/test/utils";

/** XMLHttpRequest-helyettesítő: a teszt dönti el, mikor és milyen eredménnyel ér véget a feltöltés. */
class FakeXhr {
    static instances = [];
    constructor() {
        this.listeners = {};
        this.uploadListeners = {};
        this.headers = {};
        this.upload = { addEventListener: (e, h) => (this.uploadListeners[e] = h) };
        FakeXhr.instances.push(this);
    }
    addEventListener(e, h) {
        this.listeners[e] = h;
    }
    open(method, url) {
        this.method = method;
        this.url = url;
    }
    setRequestHeader(k, v) {
        this.headers[k] = v;
    }
    send(body) {
        this.body = body;
    }
    finish(status, responseText = "{}") {
        this.status = status;
        this.responseText = responseText;
        this.listeners.load();
    }
}

const mp4 = () => new File(["x".repeat(2048)], "matrix.mp4", { type: "video/mp4" });

const renderPage = () =>
    renderWithProviders(<AdminVideoUpload />, {
        route: "/admin/movies/7/video",
        path: "/admin/movies/:movieId/video",
        routes: <Route path="/admin/movies" element={<p>admin movies</p>} />,
    });

const fileInput = () => document.getElementById("videoFile");

describe("AdminVideoUpload oldal", () => {
    beforeEach(() => {
        loginAs({ role: "Admin" });
        FakeXhr.instances = [];
        vi.stubGlobal("XMLHttpRequest", FakeXhr);
    });

    it("videó nélküli filmhez MP4-et tölt fel, haladással", async () => {
        const routes = { "GET /api/Movie/7": { id: 7, title: "Matrix" } };
        mockApi(routes);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("Matrix · Movie ID: 7")).toBeInTheDocument();
        expect(screen.getByText("Upload Video", { selector: "div" })).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Upload Video/ })).toBeDisabled();

        // Nem MP4 fájl elutasítva
        fireEvent.change(fileInput(), { target: { files: [new File(["x"], "a.avi", { type: "video/avi" })] } });
        expect(screen.getByText("Only MP4 files are supported")).toBeInTheDocument();
        fireEvent.change(fileInput(), { target: { files: [] } });

        await user.upload(fileInput(), mp4());
        expect(screen.getByText("matrix.mp4")).toBeInTheDocument();
        expect(screen.queryByText("Only MP4 files are supported")).not.toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Upload Video/ }));
        const xhr = FakeXhr.instances[0];
        expect(xhr.url).toMatch(/\/api\/admin\/Video\/upload\/7$/);
        expect(xhr.headers.Authorization).toMatch(/^Bearer /);
        expect(xhr.body.get("videoFile").name).toBe("matrix.mp4");

        act(() => xhr.uploadListeners.progress({ lengthComputable: true, loaded: 1, total: 4 }));
        expect(screen.getByText("25%")).toBeInTheDocument();
        act(() => xhr.uploadListeners.progress({ lengthComputable: false }));

        routes["GET /api/Movie/7"] = { id: 7, title: "Matrix", videoFileName: "7.mp4" };
        routes["GET /api/admin/Video/7/info"] = {
            hasVideo: true, videoFileName: "7.mp4", fileSizeMB: 12.345, originalExists: true,
            transcodingComplete: false, transcodedVersions: { "480p": true, "720p": false, "1080p": false },
        };
        act(() => xhr.finish(200));
        expect(screen.getByText(/Video uploaded successfully!/)).toBeInTheDocument();
        expect(await screen.findByText("Processing...")).toBeInTheDocument();
        expect(screen.getByText("12.35 MB")).toBeInTheDocument();
        expect(screen.getAllByText("Pending")).toHaveLength(2);

        await user.click(screen.getByRole("button", { name: "Close" }));
        expect(screen.queryByText(/Video uploaded successfully!/)).not.toBeInTheDocument();
    });

    it("feltöltési hibák: szerverüzenet, alapértelmezett, hálózati, kivétel", async () => {
        mockApi({ "GET /api/Movie/7": { id: 7 } });
        const user = userEvent.setup();
        renderPage();
        expect(await screen.findByText("Movie ID: 7")).toBeInTheDocument();

        await user.upload(fileInput(), mp4());
        await user.click(screen.getByRole("button", { name: /Upload Video/ }));
        act(() => FakeXhr.instances[0].finish(400, JSON.stringify({ message: "Too large" })));
        expect(screen.getByText("Too large")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Upload Video/ }));
        act(() => FakeXhr.instances[1].finish(500, "{}"));
        expect(screen.getByText("Upload failed")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Upload Video/ }));
        act(() => FakeXhr.instances[2].listeners.error());
        expect(screen.getByText("Network error during upload")).toBeInTheDocument();

        vi.stubGlobal("XMLHttpRequest", function Broken() {
            throw new Error("no xhr");
        });
        await user.click(screen.getByRole("button", { name: /Upload Video/ }));
        expect(screen.getByText("Error uploading video")).toBeInTheDocument();
    });

    it("kész HLS-videót mutat, törölhető; transzkódolás közben magától frissül", async () => {
        const routes = {
            "GET /api/Movie/7": { id: 7, title: "Matrix", videoFileName: "7/master.m3u8" },
            "GET /api/admin/Video/7/info": {
                hasVideo: true, videoFileName: "7/master.m3u8", fileSizeMB: 1, originalExists: false,
                transcodingComplete: true, manifestExists: true, transcodedVersions: { "480p": true, "720p": true, "1080p": true },
            },
            "DELETE /api/admin/Video/7": {},
        };
        const api = mockApi(routes);
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
        const user = userEvent.setup();
        renderPage();

        expect(await screen.findByText("HLS Adaptive Streaming Ready")).toBeInTheDocument();
        expect(screen.getByText("Missing")).toBeInTheDocument();
        expect(screen.getByText("Replace Video", { selector: "div" })).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Delete All Video Files/ }));
        expect(api.find("DELETE", "/api/admin/Video/7")).toHaveLength(0);

        confirm.mockReturnValue(true);
        await user.click(screen.getByRole("button", { name: /Delete All Video Files/ }));
        expect(await screen.findByText("Video deleted successfully")).toBeInTheDocument();

        routes["DELETE /api/admin/Video/7"] = { status: 400, body: { message: "Locked" } };
        await user.click(screen.getByRole("button", { name: /Delete All Video Files/ }));
        expect(await screen.findByText("Locked")).toBeInTheDocument();
        routes["DELETE /api/admin/Video/7"] = { status: 400, body: {} };
        await user.click(screen.getByRole("button", { name: /Delete All Video Files/ }));
        expect(await screen.findByText("Delete failed")).toBeInTheDocument();
        routes["DELETE /api/admin/Video/7"] = new Error("down");
        await user.click(screen.getByRole("button", { name: /Delete All Video Files/ }));
        expect(await screen.findByText("Error deleting video")).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /Back to Movies/ }));
        expect(screen.getByText("admin movies")).toBeInTheDocument();
    });

    it("info-végpont hibájánál a film adataiból következtet; 10 mp-enként frissít", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        try {
            const api = mockApi({
                "GET /api/Movie/7": { id: 7, videoFileName: "7.mp4" },
                "GET /api/admin/Video/7/info": { status: 404 },
            });
            renderPage();
            expect(await screen.findByText("Processing...")).toBeInTheDocument();
            const before = api.find("GET", "/api/Movie/7").length;
            // Az időzítő a betöltés utáni effektben jön létre, ezért aszinkron léptetés kell
            await act(async () => vi.advanceTimersByTimeAsync(11_000));
            await waitFor(() => expect(api.find("GET", "/api/Movie/7").length).toBeGreaterThan(before));
        } finally {
            vi.useRealTimers();
        }
    });

    it("info-végpont hálózati hibájánál is visszaesik; a film betöltési hibáját jelzi", async () => {
        const routes = {
            "GET /api/Movie/7": { id: 7, videoFileName: "7/master.m3u8" },
            "GET /api/admin/Video/7/info": new Error("down"),
        };
        mockApi(routes);
        const { unmount } = renderPage();
        expect(await screen.findByText("Complete")).toBeInTheDocument();
        unmount();

        routes["GET /api/Movie/7"] = new Error("down");
        renderPage();
        expect(await screen.findByText("Error loading movie information")).toBeInTheDocument();
    });
});
