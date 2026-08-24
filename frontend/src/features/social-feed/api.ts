import { apiClient } from "../../services/apiClient";

export type FeedTab = "ForYou" | "Following" | "Trending";
export type PostType = "Text" | "Code" | "Image" | "ProjectShare" | "Achievement" | "Deployment" | "Learning";
export interface FeedAuthor { id: string; publicId: string; userName: string; displayName: string; avatarUrl?: string; }
export interface FeedPost {
  id: string; type: PostType; content: string; codeLanguage?: string; imageUrl?: string;
  author: FeedAuthor; project?: { id: string; name: string }; likeCount: number; commentCount: number;
  saveCount: number; shareCount: number; isLiked: boolean; isSaved: boolean; isOwner: boolean;
  createdAt: string; updatedAt: string;
}
export interface FeedPage { items: FeedPost[]; nextCursor?: string; }
export interface FeedComment { id: string; postId: string; parentCommentId?: string; content: string; author: FeedAuthor; isOwner: boolean; createdAt: string; updatedAt: string; }
export interface CommentPage { items: FeedComment[]; nextCursor?: string; }
export interface CreatePostInput { type: PostType; content: string; codeLanguage?: string; imageUrl?: string; projectId?: string; }
export interface ToggleState { active: boolean; count: number; }
export type DiscoverSort = "Trending" | "Popularity" | "Recent";
export interface DiscoverFilters { search?:string; technology?:string; language?:string; sort?:DiscoverSort; limit?:number; }
export interface DiscoverPackage { id:string;slug:string;title:string;description:string;category:"ProjectTemplate"|"AiAgent"|"Theme";tags:string[];likes:number;downloads:number;publishedAt:string; }
export interface SocialDiscover { developers:Array<FeedAuthor&{followers:number;posts:number}>;projects:Array<{id:string;name:string;description?:string;ownerPublicId:string;saves:number}>;snippets:Array<{id:string;content:string;language:string;author:FeedAuthor;likes:number;saves:number;createdAt:string}>;templates:DiscoverPackage[];agents:DiscoverPackage[];themes:DiscoverPackage[];topics:Array<{name:string;posts:number}>;rankingExplanation:string; }

export const feedKeys = {
  all: ["social-feed"] as const,
  list: (tab: FeedTab) => ["social-feed", tab] as const,
  saved: ["social-feed", "saved"] as const,
  comments: (postId: string) => ["social-feed", "comments", postId] as const,
  discover: ["social-feed","discover"] as const,
};

export const socialFeedApi = {
  feed: (tab: FeedTab, cursor?: string) => apiClient.get<FeedPage>(`/feed?tab=${tab}${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ""}`),
  saved: (cursor?: string) => apiClient.get<FeedPage>(`/feed/saved${cursor ? `?cursor=${encodeURIComponent(cursor)}` : ""}`),
  discover:(filters:DiscoverFilters={})=>{const query=new URLSearchParams(); Object.entries(filters).forEach(([key,value])=>value!==undefined&&value!==""&&query.set(key,String(value))); return apiClient.get<SocialDiscover>(`/feed/discover${query.size?`?${query}`:""}`);},
  create: (input: CreatePostInput) => apiClient.post<FeedPost>("/feed", input),
  update: (postId: string, input: Pick<CreatePostInput, "content" | "codeLanguage" | "imageUrl">) => apiClient.put<FeedPost>(`/feed/posts/${postId}`, input),
  remove: (postId: string) => apiClient.delete<void>(`/feed/posts/${postId}`),
  like: (postId: string) => apiClient.post<ToggleState>(`/feed/posts/${postId}/like`),
  save: (postId: string) => apiClient.post<ToggleState>(`/feed/posts/${postId}/save`),
  share: (postId: string) => apiClient.post<ToggleState>(`/feed/posts/${postId}/share`),
  comments: (postId: string, cursor?: string) => apiClient.get<CommentPage>(`/feed/posts/${postId}/comments${cursor ? `?cursor=${encodeURIComponent(cursor)}` : ""}`),
  comment: (postId: string, content: string, parentCommentId?: string) => apiClient.post<FeedComment>(`/feed/posts/${postId}/comments`, { content, parentCommentId }),
  removeComment: (commentId: string) => apiClient.delete<void>(`/feed/comments/${commentId}`),
};
