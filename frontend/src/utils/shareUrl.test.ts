import { afterEach, describe, expect, it, vi } from "vitest";
import { shareUrl } from "./shareUrl";

describe("shareUrl", () => {
  afterEach(() => vi.restoreAllMocks());

  it("uses native sharing when available", async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: share,
    });

    await expect(
      shareUrl("Project", "https://coding.test/project"),
    ).resolves.toBe("shared");
    expect(share).toHaveBeenCalledWith({
      title: "Project",
      url: "https://coding.test/project",
    });
  });

  it("copies the link when native sharing fails", async () => {
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: vi.fn().mockRejectedValue(new Error("unavailable")),
    });
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });

    await expect(
      shareUrl("Deployment", "https://coding.test/deploy/site"),
    ).resolves.toBe("copied");
    expect(writeText).toHaveBeenCalledWith("https://coding.test/deploy/site");
  });

  it("does not copy after the user cancels native sharing", async () => {
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: vi
        .fn()
        .mockRejectedValue(new DOMException("cancelled", "AbortError")),
    });
    const writeText = vi.fn();
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });

    await expect(
      shareUrl("Project", "https://coding.test/project"),
    ).resolves.toBe("cancelled");
    expect(writeText).not.toHaveBeenCalled();
  });
});
