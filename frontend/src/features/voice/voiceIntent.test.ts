import { describe, expect, it } from "vitest";
import { parseVoiceIntent } from "./voiceIntent";

describe("voice intent parser", () => {
  it("parses safe file and AI intents", () => {
    expect(parseVoiceIntent("Open AuthService.cs")).toMatchObject({ kind: "openFile", fileName: "AuthService.cs", risky: false });
    expect(parseVoiceIntent("Explain this function.")).toMatchObject({ kind: "explain", risky: false });
    expect(parseVoiceIntent("Ask AI to fix this error.")).toMatchObject({ kind: "fixError", risky: false });
  });
  it("marks execution and repository mutation as risky", () => {
    expect(parseVoiceIntent("Run tests.")).toMatchObject({ kind: "runTests", risky: true });
    expect(parseVoiceIntent("Create a branch called feature/payments.")).toMatchObject({ kind: "createBranch", branchName: "feature/payments", risky: true });
  });
  it("does not guess unsupported commands", () => expect(parseVoiceIntent("delete the production database")).toMatchObject({ kind: "unknown" }));
});
