import { describe, expect, it } from "vitest";
import { upsertConversation } from "./conversationCache";
import type { Conversation } from "./types";

const conversation = (id: string, name: string): Conversation => ({
  id,
  type: "Direct",
  name,
  participants: [],
  unreadCount: 0,
  updatedAt: "2026-08-14T10:00:00Z",
});

describe("conversation cache", () => {
  it("adds a newly-created conversation to an empty list", () => {
    expect(upsertConversation(undefined, conversation("new", "Aydan"))).toEqual([
      conversation("new", "Aydan"),
    ]);
  });

  it("moves an existing conversation to the front without duplicating it", () => {
    const updated = conversation("one", "Updated");
    expect(upsertConversation([conversation("one", "Old"), conversation("two", "Leyla")], updated))
      .toEqual([updated, conversation("two", "Leyla")]);
  });
});
