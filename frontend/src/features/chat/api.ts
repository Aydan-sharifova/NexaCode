import { apiClient } from "../../services/apiClient";
import type { Conversation, ChatMessage, MessagePage } from "./types";
export const chatApi = {
  conversations: () => apiClient.get<Conversation[]>("/chat/conversations"),
  direct: (otherUserId: string) => apiClient.post<Conversation>("/chat/conversations/direct", { otherUserId }),
  messages: (conversationId: string, cursor?: string) => apiClient.get<MessagePage>(`/chat/conversations/${conversationId}/messages?limit=30${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ""}`),
  send: (conversationId: string, content: string) => apiClient.post<ChatMessage>(`/chat/conversations/${conversationId}/messages`, { content }),
  read: (conversationId: string, throughMessageId?: string) => apiClient.post<void>(`/chat/conversations/${conversationId}/read`, { throughMessageId }),
  remove: (messageId: string) => apiClient.delete<void>(`/chat/messages/${messageId}`),
  edit: (messageId: string, content: string) => apiClient.put<ChatMessage>(`/chat/messages/${messageId}`, { content }),
  deleteConversation: (conversationId: string) => apiClient.delete<void>(`/chat/conversations/${conversationId}`),
  upload: (conversationId: string, file: File, content?: string) => { const body = new FormData(); body.append("file", file); if (content?.trim()) body.append("content", content.trim()); return apiClient.postForm<ChatMessage>(`/chat/conversations/${conversationId}/attachments`, body); },
  attachment: (attachmentId: string) => apiClient.getBlob(`/chat/attachments/${attachmentId}`),
};
