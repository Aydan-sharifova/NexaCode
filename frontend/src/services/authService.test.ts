import { beforeEach, describe, expect, it, vi } from "vitest";
import { authService } from "./authService";
import { tokenStore } from "./tokenStore";

const registration = {
  firstName: "Aydan",
  lastName: "Sharifova",
  userName: "aydanss",
  email: "aydan@example.com",
  password: "SecurePassword1!",
};

describe("authService", () => {
  beforeEach(() => {
    tokenStore.clear();
    vi.restoreAllMocks();
  });

  it("accepts an empty successful registration response", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(null, { status: 201 })
    );

    await expect(authService.register(registration)).resolves.toBeUndefined();
    expect(tokenStore.get()).toBeNull();
  });

  it("reports an invalid login response without throwing a property access error", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(null, { status: 200 })
    );

    await expect(authService.login({
      email: registration.email,
      password: registration.password,
    })).rejects.toThrow("The authentication server returned an invalid response");
  });
});
