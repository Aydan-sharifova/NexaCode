export type NotificationType = "Invitation" | "Message" | "AICompleted" | "ProjectUpdated" | "Warning" | "TaskAssignment" | "UserMention" | "DirectMessage" | "RoleChange" | "TaskDeadlineExceeded";
export interface AppNotification { id: string; userId: string; type: NotificationType; title: string; message: string; relatedEntityId?: string; relatedEntityType?: string; isRead: boolean; createdAt: string; readAt?: string; }
export interface NotificationPage { items: AppNotification[]; nextCursor?: string; unreadCount: number; }
