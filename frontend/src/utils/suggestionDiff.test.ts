import { describe, expect, it } from "vitest";
import { buildSuggestionDiff } from "./suggestionDiff";

describe("buildSuggestionDiff", () => {
  it("keeps context and marks replacement lines", () => {
    const result = buildSuggestionDiff(
      "header\nold\nfooter",
      "header\nnew\nfooter",
    );
    expect(result.lines).toEqual([
      { kind: "context", text: "header" },
      { kind: "removed", text: "old" },
      { kind: "added", text: "new" },
      { kind: "context", text: "footer" },
    ]);
    expect(result.truncated).toBe(false);
  });

  it("bounds oversized AI output", () => {
    const result = buildSuggestionDiff(
      "",
      Array.from({ length: 20 }, (_, index) => `${index}`).join("\n"),
      5,
    );
    expect(result.lines).toHaveLength(5);
    expect(result.truncated).toBe(true);
  });
});
