export type ProjectRole = "Owner" | "Admin" | "Maintainer" | "Developer" | "Viewer";
export type ProjectStatus = "Draft" | "Active" | "DeadlineSoon" | "DeadlineExpired" | "Suspended" | "Archived" | "Deleted";

export interface ProjectListItem { id: string; name: string; description?: string; defaultLanguage: string; currentUserRole: ProjectRole; memberCount: number; createdAt: string; deadlineAt?: string; status: ProjectStatus; isReadOnly: boolean; }
export interface ProjectDetails { id: string; name: string; description?: string; defaultLanguage: string; isPublic: boolean; ownerId: string; currentUserRole: ProjectRole; createdAt: string; updatedAt?: string; deadlineAt?: string; status: ProjectStatus; isReadOnly: boolean; }
export interface ProjectMember { userId: string; publicId?: string; fullName: string; email: string; avatarUrl?: string; role: ProjectRole; joinedAt: string; }
export interface ProjectInvitation { id: string; email: string; role: ProjectRole; expiresAt: string; invitedBy: string; }
export interface ProjectInput { name: string; description?: string; defaultLanguage: string; isPublic: boolean; deadlineAt?: string; }
export interface ProjectDeadlineState { projectId: string; deadlineAt: string; status: ProjectStatus; }
