import { describe, expect, it } from "vitest";
import { isValidPublicUserId, normalizePublicUserId } from "./publicUserId";

describe("public user IDs", () => {
  it.each(["594BA937", "@594BA937", "  @594ba937  "])("accepts %s", (value) => {
    expect(isValidPublicUserId(value)).toBe(true);
    expect(normalizePublicUserId(value)).toBe("594BA937");
  });

  it.each(["", "594BA93", "594BA9370", "594BAO37", "not-a-user"])("rejects %s", (value) => {
    expect(isValidPublicUserId(value)).toBe(false);
  });
});
