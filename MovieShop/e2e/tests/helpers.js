import { expect, request } from "@playwright/test";

import { API_URL } from "../playwright.config.js";

export const ADMIN = {
    email: process.env.E2E_ADMIN_EMAIL ?? "admin@movieshop.com",
    password: process.env.E2E_ADMIN_PASSWORD ?? "Admin123!",
};

export const PASSWORD = "E2e-Test123!";

export const uniqueEmail = (prefix = "e2e") =>
    `${prefix}_${Date.now()}_${Math.floor(Math.random() * 1e6)}@movieshop.test`;

/** Önálló API-kliens a gatewayhez — előkészítéshez és takarításhoz. */
export async function apiClient() {
    return request.newContext({ baseURL: API_URL });
}

export const bearer = (token) => ({ Authorization: `Bearer ${token}` });

export async function login(api, email, password) {
    const res = await api.post("/api/Auth/login", { data: { email, password } });
    expect(res.status(), `login ${email}`).toBe(200);
    return res.json();
}

export async function registerUser(api, prefix) {
    const email = uniqueEmail(prefix);
    const res = await api.post("/api/Auth/register", {
        data: { name: prefix, email, password: PASSWORD, confirmPassword: PASSWORD },
    });
    expect(res.status(), `register ${email}`).toBe(200);
    const body = await res.json();
    return { email, token: body.token, id: body.user.id };
}

export async function deleteUser(api, adminToken, userId) {
    await api.delete(`/api/User/${userId}`, { headers: bearer(adminToken) });
}

/**
 * Végleges konzisztencia: az eseményeknek időbe telik végigmenni a RabbitMQ-n.
 * A feltételt másodpercenként újraellenőrzi, legfeljebb `timeoutMs`-ig.
 */
// A felhőben a nullára skálázott fogyasztónak előbb fel kell ébrednie, ott hosszabb várakozás kell
const EVENT_TIMEOUT_MS = Number(process.env.E2E_EVENT_TIMEOUT_MS ?? 20_000);

export async function eventually(check, { timeoutMs = EVENT_TIMEOUT_MS, message } = {}) {
    await expect
        .poll(async () => {
            try {
                return await check();
            } catch {
                return false;
            }
        }, { timeout: timeoutMs, intervals: [500, 1000], message })
        .toBe(true);
}
