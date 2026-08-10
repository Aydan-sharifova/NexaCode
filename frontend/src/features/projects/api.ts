import { apiClient } from "../../services/apiClient";
import type { ProjectDetails, ProjectInput, ProjectInvitation, ProjectListItem, ProjectMember, ProjectRole } from "./types";

export const projectApi = {
  list: () => apiClient.get<ProjectListItem[]>("/projects"),
  details: (id: string) => apiClient.get<ProjectDetails>(`/projects/${id}`),
  create: (input: ProjectInput) => apiClient.post<ProjectDetails>("/projects", input),
  update: (id: string, input: ProjectInput) => apiClient.put<ProjectDetails>(`/projects/${id}`, input),
  remove: (id: string) => apiClient.delete<void>(`/projects/${id}`),
  members: (id: string) => apiClient.get<ProjectMember[]>(`/projects/${id}/members`),
  invitations: (id: string) => apiClient.get<ProjectInvitation[]>(`/projects/${id}/invitations`),
  invite: (id: string, email: string, role: Exclude<ProjectRole, "Owner">) => apiClient.post<{ id: string; token: string; expiresAt: string }>(`/projects/${id}/invitations`, { email, role }),
  changeRole: (id: string, userId: string, role: Exclude<ProjectRole, "Owner">) => apiClient.put<void>(`/projects/${id}/members/${userId}/role`, { role }),
  removeMember: (id: string, userId: string) => apiClient.delete<void>(`/projects/${id}/members/${userId}`),
  acceptInvitation: (token: string) => apiClient.post<{ projectId: string }>("/projects/invitations/accept", { token }),
  rejectInvitation: (token: string) => apiClient.post<void>("/projects/invitations/reject", { token }),
  acceptInvitationById: (id: string) => apiClient.post<{ projectId: string }>(`/projects/invitations/${id}/accept`),
  rejectInvitationById: (id: string) => apiClient.post<void>(`/projects/invitations/${id}/reject`),
};
