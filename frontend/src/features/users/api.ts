import { apiClient } from "../../services/apiClient";

export interface UserSearchResult { publicId: string; displayName: string; userName: string; avatarUrl?: string; bio?: string; publicProjectCount: number; }
export interface UserSearchPage { items: UserSearchResult[]; page: number; pageSize: number; hasMore: boolean; }
export interface PublicUserProfile extends UserSearchResult { joinedAt: string; }
export interface PublicProject { id: string; name: string; description?: string; defaultLanguage: string; updatedAt: string; }
export interface PublicProjectPage { items: PublicProject[]; page: number; pageSize: number; hasMore: boolean; }
export interface PublicProjectDetails extends PublicProject { ownerPublicId?: string; ownerDisplayName: string; createdAt: string; }
export interface PublicProjectNode { id: string; parentId?: string; name: string; nodeType: "File" | "Folder"; path: string; hasChildren: boolean; }
export interface PublicProjectFile { id: string; path: string; content: string; versionNumber: number; updatedAt: string; }

export const userKeys = {
  search: (query: string) => ["users", "search", query] as const,
  profile: (publicId: string) => ["users", "profile", publicId] as const,
  publicProjects: (publicId: string) => ["users", "public-projects", publicId] as const,
  publicProject: (projectId: string) => ["public-project", projectId] as const,
};

export const usersApi = {
  search: (query: string, signal?: AbortSignal) => apiClient.get<UserSearchPage>(`/users/search?q=${encodeURIComponent(query)}&pageSize=20`, { signal }),
  profile: (publicId: string) => apiClient.get<PublicUserProfile>(`/users/${encodeURIComponent(publicId)}/profile`),
  publicProjects: (publicId: string) => apiClient.get<PublicProjectPage>(`/users/${encodeURIComponent(publicId)}/projects/public`),
  publicProject: (projectId: string) => apiClient.get<PublicProjectDetails>(`/public-projects/${projectId}`),
  publicProjectTree: (projectId: string) => apiClient.get<PublicProjectNode[]>(`/public-projects/${projectId}/tree`),
  publicProjectFile: (projectId: string, nodeId: string) => apiClient.get<PublicProjectFile>(`/public-projects/${projectId}/files/${nodeId}`),
};
