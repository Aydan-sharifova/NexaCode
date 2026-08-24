import { describe, expect, it } from "vitest";
import { deduplicateById } from "./deduplicate";

describe("deduplicateById", () => {
  it("keeps the first occurrence in stable feed order", () => {
    expect(deduplicateById([{ id: "2", value: "new" }, { id: "1", value: "one" }, { id: "2", value: "old" }]))
      .toEqual([{ id: "2", value: "new" }, { id: "1", value: "one" }]);
  });
});
