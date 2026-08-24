import { apiClient } from "../../services/apiClient";

export interface UserSearchResult { publicId: string; displayName: string; userName: string; avatarUrl?: string; bio?: string; publicProjectCount: number; }
export interface UserSearchPage { items: UserSearchResult[]; page: number; pageSize: number; hasMore: boolean; }
export interface PublicUserProfile extends UserSearchResult {
  id: string;
  joinedAt: string;
  coverImageUrl?: string;
  headline?: string;
  location?: string;
  websiteUrl?: string;
  gitHubUrl?: string;
  linkedInUrl?: string;
  portfolioUrl?: string;
  primaryRole?: string;
  experienceLevel?: string;
  skills: string[];
  learningTopics: string[];
  followerCount?: number;
  followingCount?: number;
  isFollowing: boolean;
  isBlockedByMe: boolean;
  isOwnProfile: boolean;
  isProfilePublic: boolean;
  isActivityPublic: boolean;
  areFollowersPublic: boolean;
}
export interface PortfolioPost { id:string;type:string;content:string;codeLanguage?:string;imageUrl?:string;projectId?:string;projectName?:string;likes:number;comments:number;saves:number;shares:number;createdAt:string; }
export interface PortfolioActivity { type:string;title:string;description:string;occurredAt:string;evidenceId?:string; }
export interface PortfolioPerson { publicId:string;userName:string;displayName:string;avatarUrl?:string; }
export interface ContributionSummary { commits:number;mergedPullRequests:number;acceptedReviews:number;publishedProjects:number;usefulSnippets:number;deployments:number;communityPosts:number;verifiedAchievements:number; }
export interface DeveloperPortfolio { activityVisible:boolean;posts:PortfolioPost[];snippets:PortfolioPost[];activity:PortfolioActivity[];contributions?:ContributionSummary;followers:PortfolioPerson[];following:PortfolioPerson[]; }
export interface FollowState { isFollowing: boolean; followerCount?: number; }
export interface BlockState { isBlocked: boolean; }
export interface BlockedUser { publicId: string; displayName: string; userName: string; avatarUrl?: string; blockedAt: string; }
export interface BlockedUserPage { items: BlockedUser[]; nextCursor?: string; }
export interface UpdateDeveloperProfileInput {
  displayName: string;
  bio?: string;
  headline?: string;
  location?: string;
  websiteUrl?: string;
  gitHubUrl?: string;
  linkedInUrl?: string;
  portfolioUrl?: string;
  primaryRole?: string;
  experienceLevel?: string;
  skills?: string[];
  learningTopics?: string[];
  isProfilePublic: boolean;
  isActivityPublic: boolean;
  areFollowersPublic: boolean;
}
export interface PublicProject { id: string; name: string; description?: string; defaultLanguage: string; updatedAt: string; }
export interface PublicProjectPage { items: PublicProject[]; page: number; pageSize: number; hasMore: boolean; }
export interface PublicProjectDetails extends PublicProject { ownerPublicId?: string; ownerDisplayName: string; createdAt: string; }
export interface PublicProjectNode { id: string; parentId?: string; name: string; nodeType: "File" | "Folder"; path: string; hasChildren: boolean; }
export interface PublicProjectFile { id: string; path: string; content: string; versionNumber: number; updatedAt: string; }

export const userKeys = {
  search: (query: string) => ["users", "search", query] as const,
  profile: (publicId: string) => ["users", "profile", publicId] as const,
  publicProjects: (publicId: string) => ["users", "public-projects", publicId] as const,
  portfolio: (publicId:string)=>["users","portfolio",publicId] as const,
  publicProject: (projectId: string) => ["public-project", projectId] as const,
};

export const usersApi = {
  search: (query: string, signal?: AbortSignal) => apiClient.get<UserSearchPage>(`/users/search?q=${encodeURIComponent(query)}&pageSize=20`, { signal }),
  profile: (publicId: string) => apiClient.get<PublicUserProfile>(`/users/${encodeURIComponent(publicId)}/profile`),
  portfolio:(publicId:string)=>apiClient.get<DeveloperPortfolio>(`/users/${encodeURIComponent(publicId)}/portfolio`),
  updateProfile: (value: UpdateDeveloperProfileInput) => apiClient.put<PublicUserProfile>("/users/profile", value),
  follow: (publicId: string) => apiClient.post<FollowState>(`/users/${encodeURIComponent(publicId)}/follow`, {}),
  unfollow: (publicId: string) => apiClient.delete<FollowState>(`/users/${encodeURIComponent(publicId)}/follow`),
  block: (publicId: string) => apiClient.post<BlockState>(`/users/${encodeURIComponent(publicId)}/block`),
  unblock: (publicId: string) => apiClient.delete<BlockState>(`/users/${encodeURIComponent(publicId)}/block`),
  blocked: () => apiClient.get<BlockedUserPage>("/users/blocked"),
  publicProjects: (publicId: string) => apiClient.get<PublicProjectPage>(`/users/${encodeURIComponent(publicId)}/projects/public`),
  publicProject: (projectId: string) => apiClient.get<PublicProjectDetails>(`/public-projects/${projectId}`),
  publicProjectTree: (projectId: string) => apiClient.get<PublicProjectNode[]>(`/public-projects/${projectId}/tree`),
  publicProjectFile: (projectId: string, nodeId: string) => apiClient.get<PublicProjectFile>(`/public-projects/${projectId}/files/${nodeId}`),
};
