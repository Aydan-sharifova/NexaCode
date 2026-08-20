export type ProjectRole = "Owner" | "Admin" | "Member";

export interface ProjectListItem { id: string; name: string; description?: string; defaultLanguage: string; currentUserRole: ProjectRole; memberCount: number; createdAt: string; }
export interface ProjectDetails { id: string; name: string; description?: string; defaultLanguage: string; isPublic: boolean; ownerId: string; currentUserRole: ProjectRole; createdAt: string; updatedAt?: string; }
export interface ProjectMember { userId: string; publicId?: string; fullName: string; email: string; avatarUrl?: string; role: ProjectRole; joinedAt: string; }
export interface ProjectInvitation { id: string; email: string; role: ProjectRole; expiresAt: string; invitedBy: string; }
export interface ProjectInput { name: string; description?: string; defaultLanguage: string; isPublic: boolean; }
