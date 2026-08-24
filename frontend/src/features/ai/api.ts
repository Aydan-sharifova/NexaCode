import { apiClient, ApiError } from "../../services/apiClient";
import { tokenStore } from "../../services/tokenStore";
import { API_URL } from "../../services/deployment";
import type {
  AiAssistantRequest,
  AiConversation,
  AiConversationDetails,
  AiStreamChunk,
  GuestAiRequest,
} from "./types";

async function streamRequest(
  request: AiAssistantRequest,
  signal: AbortSignal,
  onChunk: (chunk: AiStreamChunk) => void,
  retry = true,
): Promise<void> {
  const token = tokenStore.get();
  let response: Response;
  try {
    response = await fetch(`${API_URL}/ai/stream`, {
      method: "POST",
      credentials: "include",
      signal,
      headers: {
        "Content-Type": "application/json",
        Accept: "text/event-stream",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(request),
    });
  } catch (error) {
    if (signal.aborted) throw error;
    throw new ApiError("The AI service is unavailable.", 0);
  }

  if (response.status === 401 && retry) {
    await apiClient.refreshSession();
    return streamRequest(request, signal, onChunk, false);
  }
  if (!response.ok) throw new ApiError((await response.text()) || "AI request failed.", response.status);
  await consumeStream(response, onChunk);
}

async function guestStreamRequest(
  request: GuestAiRequest,
  signal: AbortSignal,
  onChunk: (chunk: AiStreamChunk) => void,
): Promise<void> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}/ai/guest/stream`, {
      method: "POST",
      credentials: "omit",
      signal,
      headers: {
        "Content-Type": "application/json",
        Accept: "text/event-stream",
      },
      body: JSON.stringify(request),
    });
  } catch (error) {
    if (signal.aborted) throw error;
    throw new ApiError("The guest AI service is unavailable.", 0);
  }

  if (!response.ok) {
    const fallback = response.status === 429
      ? "The guest preview is busy. Please wait a minute and try again."
      : "Guest AI request failed.";
    throw new ApiError((await response.text()) || fallback, response.status);
  }
  await consumeStream(response, onChunk);
}

async function consumeStream(
  response: Response,
  onChunk: (chunk: AiStreamChunk) => void,
): Promise<void> {
  if (!response.body) throw new ApiError("The AI stream was empty.", 502);

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    const events = buffer.split("\n\n");
    buffer = events.pop() ?? "";
    for (const event of events) {
      const data = event.split("\n").find((line) => line.startsWith("data:"));
      if (data) {
        const chunk = JSON.parse(data.slice(5).trim()) as AiStreamChunk;
        if (chunk.error) throw new ApiError(chunk.error, 502);
        onChunk(chunk);
      }
    }
  }
}

export const aiApi = {
  conversations: (projectId: string) => apiClient.get<AiConversation[]>(`/ai/projects/${projectId}/conversations`),
  conversation: (id: string) => apiClient.get<AiConversationDetails>(`/ai/conversations/${id}`),
  stream: streamRequest,
  guestStream: guestStreamRequest,
};
