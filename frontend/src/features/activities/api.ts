import { apiClient } from "../../services/apiClient";
export interface ActivityLog { id: string; userId?: string; userName?: string; projectId?: string; projectName?: string; actionType: string; entityType: string; entityId?: string; description: string; metadata: Record<string, unknown>; ipAddress?: string; userAgent?: string; createdAt: string; }
export interface ActivityPage { items: ActivityLog[]; total: number; page: number; pageSize: number; }
export interface ActivityFilters { userId?: string; projectId?: string; actionType?: string; entityType?: string; from?: string; to?: string; page?: number; }
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
export const isGuid = (value: string) => guidPattern.test(value.trim());
export const activityApi = { list: (filters: ActivityFilters) => { const query = new URLSearchParams(); Object.entries(filters).forEach(([key, value]) => { if (!value || ((key === "userId" || key === "projectId") && !isGuid(String(value)))) return; query.set(key, String(value)); }); return apiClient.get<ActivityPage>(`/admin/activities?${query}`); } };
