import { describe, expect, it } from "vitest";
import { isGuid } from "./api";

describe("activity ID filters", () => {
  it("accepts complete UUIDs", () => {
    expect(isGuid("5734eb16-f6c5-4173-8568-7c325af30f3a")).toBe(true);
  });

  it("rejects partial IDs before they reach the API", () => {
    expect(isGuid("2AF936E8")).toBe(false);
  });
});
