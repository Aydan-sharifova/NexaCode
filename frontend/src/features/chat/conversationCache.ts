import type { Conversation } from "./types";

export const conversationsQueryKey = ["chat-conversations"] as const;

export function upsertConversation(
  conversations: Conversation[] | undefined,
  incoming: Conversation,
): Conversation[] {
  return [incoming, ...(conversations ?? []).filter((item) => item.id !== incoming.id)];
}
