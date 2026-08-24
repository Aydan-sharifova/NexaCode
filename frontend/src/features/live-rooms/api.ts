import { apiClient } from "../../services/apiClient";
import type { InterviewerNote, LiveRoomDetails, LiveRoomMessage, LiveRoomMode, LiveRoomReaction, LiveRoomRole, LiveRoomStatus, LiveRoomSummary, LiveRoomTask, LiveRoomVisibility } from "./types";

export interface CreateLiveRoomInput { projectId?: string; title: string; description?: string; mode: LiveRoomMode; visibility: LiveRoomVisibility; challengeType?: string; problemTitle?: string; problemStatement?: string; durationMinutes?: number; scheduledAt?: string; }
export const liveRoomsApi = {
  list: () => apiClient.get<LiveRoomSummary[]>("/live-rooms"),
  details: (id: string) => apiClient.get<LiveRoomDetails>(`/live-rooms/${id}`),
  create: (input: CreateLiveRoomInput) => apiClient.post<LiveRoomDetails>("/live-rooms", input),
  invite: (id: string, userPublicId: string, role: LiveRoomRole) => apiClient.post<LiveRoomDetails>(`/live-rooms/${id}/participants`, { userPublicId, role }),
  join: (id: string) => apiClient.post<LiveRoomDetails>(`/live-rooms/${id}/join`, {}),
  leave: (id: string) => apiClient.post<void>(`/live-rooms/${id}/leave`, {}),
  status: (id: string, status: LiveRoomStatus, expectedStateVersion: number) => apiClient.put<LiveRoomDetails>(`/live-rooms/${id}/status`, { status, expectedStateVersion }),
  messages: (id: string) => apiClient.get<LiveRoomMessage[]>(`/live-rooms/${id}/messages`),
  send: (id: string, content: string) => apiClient.post<LiveRoomMessage>(`/live-rooms/${id}/messages`, { content }),
  tasks: (id: string) => apiClient.get<LiveRoomTask[]>(`/live-rooms/${id}/tasks`),
  createTask: (id: string, title: string, description?: string) => apiClient.post<LiveRoomTask>(`/live-rooms/${id}/tasks`, { title, description }),
  setTaskStatus: (id: string, taskId: string, status: LiveRoomTask["status"]) => apiClient.put<LiveRoomTask>(`/live-rooms/${id}/tasks/${taskId}/status`, { status }),
  react: (id: string, emoji: string) => apiClient.post<LiveRoomReaction>(`/live-rooms/${id}/reactions`, { emoji }),
  notes: (id: string) => apiClient.get<InterviewerNote[]>(`/live-rooms/${id}/interviewer-notes`),
  saveNote: (id: string, content: string, noteId?: string) => apiClient.post<InterviewerNote>(`/live-rooms/${id}/interviewer-notes`, { noteId, content }),
};
