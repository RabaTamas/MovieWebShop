import { orderService } from "./orderService";
import API_BASE_URL from "../config/api";

const jsonResponse = (body, ok = true) => ({ ok, json: async () => body });

describe("orderService", () => {
    let fetchMock;

    beforeEach(() => {
        fetchMock = vi.fn();
        vi.stubGlobal("fetch", fetchMock);
    });

    afterEach(() => vi.unstubAllGlobals());

    it("a rendeléseket Bearer tokennel kéri le a gatewaytől", async () => {
        fetchMock.mockResolvedValue(jsonResponse([{ id: 1 }]));

        const orders = await orderService.getAllOrders("tok3n");

        expect(orders).toEqual([{ id: 1 }]);
        expect(fetchMock).toHaveBeenCalledWith(`${API_BASE_URL}/api/admin/orders`, {
            headers: { Authorization: "Bearer tok3n" },
        });
    });

    it("az állapotváltást PUT kéréssel, JSON törzzsel küldi", async () => {
        fetchMock.mockResolvedValue(jsonResponse(null));

        const ok = await orderService.updateOrderStatus(12, "Completed", "tok3n");

        expect(ok).toBe(true);
        const [url, options] = fetchMock.mock.calls[0];
        expect(url).toBe(`${API_BASE_URL}/api/admin/orders/12/status`);
        expect(options.method).toBe("PUT");
        expect(options.headers["Content-Type"]).toBe("application/json");
        expect(JSON.parse(options.body)).toEqual({ status: "Completed" });
    });

    it("állapot szerint szűrt listát kér", async () => {
        fetchMock.mockResolvedValue(jsonResponse([]));

        await orderService.getOrdersByStatus("Pending", "tok3n");

        expect(fetchMock.mock.calls[0][0]).toBe(`${API_BASE_URL}/api/admin/orders/status/Pending`);
    });

    it.each([
        ["getAllOrders", ["t"], "Failed to fetch orders"],
        ["getOrderById", [1, "t"], "Failed to fetch order"],
        ["updateOrderStatus", [1, "Completed", "t"], "Failed to update order status"],
        ["getOrderStatistics", ["t"], "Failed to fetch order statistics"],
    ])("%s hibás válasznál kivételt dob", async (method, args, message) => {
        fetchMock.mockResolvedValue(jsonResponse({}, false));

        await expect(orderService[method](...args)).rejects.toThrow(message);
    });
});
