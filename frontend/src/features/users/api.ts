import { apiClient } from "../../services/apiClient";

export interface UserSearchResult { publicId: string; displayName: string; userName: string; avatarUrl?: string; bio?: string; publicProjectCount: number; }
export interface UserSearchPage { items: UserSearchResult[]; page: number; pageSize: number; hasMore: boolean; }
export interface PublicUserProfile extends UserSearchResult { joinedAt: string; }
export interface PublicProject { id: string; name: string; description?: string; defaultLanguage: string; updatedAt: string; }
export interface PublicProjectPage { items: PublicProject[]; page: number; pageSize: number; hasMore: boolean; }

export const userKeys = {
  search: (query: string) => ["users", "search", query] as const,
  profile: (publicId: string) => ["users", "profile", publicId] as const,
  publicProjects: (publicId: string) => ["users", "public-projects", publicId] as const,
};

export const usersApi = {
  search: (query: string, signal?: AbortSignal) => apiClient.get<UserSearchPage>(`/users/search?q=${encodeURIComponent(query)}&pageSize=20`, { signal }),
  profile: (publicId: string) => apiClient.get<PublicUserProfile>(`/users/${encodeURIComponent(publicId)}/profile`),
  publicProjects: (publicId: string) => apiClient.get<PublicProjectPage>(`/users/${encodeURIComponent(publicId)}/projects/public`),
};
