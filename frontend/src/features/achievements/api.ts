import { apiClient } from "../../services/apiClient";

export interface AchievementItem { id: string; code: string; title: string; description: string; icon: string; category: string; points: number; unlocked: boolean; verified: boolean; unlockedAt?: string; evidenceType?: string; evidenceId?: string; progress: number; target: number; }
export interface AchievementProfile { userId: string; reputationScore: number; contributionLevel: string; unlockedCount: number; totalCount: number; achievements: AchievementItem[]; }
export interface DeveloperJourneyItem { code: string; title: string; description: string; occurredAt: string; evidenceId?: string; }
export const achievementsApi = {
  mine: () => apiClient.get<AchievementProfile>("/achievements/me"),
  user: (publicId: string) => apiClient.get<AchievementProfile>(`/achievements/users/${encodeURIComponent(publicId)}`),
  journey: (publicId: string) => apiClient.get<DeveloperJourneyItem[]>(`/achievements/users/${encodeURIComponent(publicId)}/journey`),
};
