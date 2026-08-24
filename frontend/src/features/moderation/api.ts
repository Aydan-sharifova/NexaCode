import { apiClient } from "../../services/apiClient";

export type ReportTargetType = "Post" | "Comment" | "Project" | "Snippet" | "Profile";
export type ReportState = "Pending" | "Reviewing" | "ActionTaken" | "Dismissed";
export type ModerationAction = "StartReview" | "Dismiss" | "RemoveContent" | "SuspendProfile" | "RestoreToPending";
export interface ModerationUser { id: string; publicId: string; userName: string; fullName: string; }
export interface ModerationActionItem { id: string; moderator: ModerationUser; action: ModerationAction; previousState: ReportState; newState: ReportState; note: string; createdAt: string; }
export interface ContentReport { id: string; reporter: ModerationUser; targetType: ReportTargetType; targetId: string; targetLabel: string; reason: string; details?: string; state: ReportState; assignedModerator?: ModerationUser; createdAt: string; reviewedAt?: string; actions: ModerationActionItem[]; }
export interface ModerationQueue { items: ContentReport[]; total: number; page: number; pageSize: number; }

export const moderationApi = {
  report: (targetType: ReportTargetType, targetId: string, reason: string, details?: string) => apiClient.post<ContentReport>("/reports", { targetType, targetId, reason, details }),
  mine: () => apiClient.get<ModerationQueue>("/reports/mine"),
  queue: (state?: ReportState, targetType?: ReportTargetType, page = 1) => apiClient.get<ModerationQueue>(`/moderation/reports?${new URLSearchParams({ ...(state ? { state } : {}), ...(targetType ? { targetType } : {}), page: String(page), pageSize: "30" })}`),
  act: (reportId: string, action: ModerationAction, note: string) => apiClient.post<ContentReport>(`/moderation/reports/${reportId}/actions`, { action, note }),
};
