import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "./apiClient";
import { tokenStore } from "./tokenStore";

const user = { id: "1", firstName: "A", lastName: "User", userName: "a", email: "a@test.local", isEmailVerified: true, roles: ["User"], isDemo: false };

describe("apiClient session rotation", () => {
  beforeEach(() => { tokenStore.clear(); vi.restoreAllMocks(); });

  it("coalesces concurrent proactive refresh and sends the rotated token", async () => {
    tokenStore.set("expired", new Date(Date.now() - 1000).toISOString());
    let refreshes = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/auth/refresh")) { refreshes++; await Promise.resolve(); return new Response(JSON.stringify({ accessToken: "rotated", accessTokenExpiresAt: new Date(Date.now() + 60_000).toISOString(), user }), { status: 200 }); }
      return new Response(JSON.stringify({ authorization: new Headers(init?.headers).get("Authorization") }), { status: 200 });
    }));

    const [first, second] = await Promise.all([apiClient.get<{authorization:string}>("/projects"), apiClient.get<{authorization:string}>("/users")]);
    expect(refreshes).toBe(1);
    expect(first.authorization).toBe("Bearer rotated");
    expect(second.authorization).toBe("Bearer rotated");
  });

  it("clears stale credentials and publishes expiry when refresh is rejected", async () => {
    tokenStore.set("expired", new Date(Date.now() - 1000).toISOString());
    const expired = vi.fn(); window.addEventListener("coding:session-expired", expired, { once: true });
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 401 })));

    await expect(apiClient.get("/projects")).rejects.toMatchObject({ status: 401 });
    expect(tokenStore.get()).toBeNull();
    expect(expired).toHaveBeenCalledOnce();
  });
});
