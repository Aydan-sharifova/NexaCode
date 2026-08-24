export interface AnalyticsSummary { activeUsers: number; projectsCreated: number; taskCompletionRate: number; fileChanges: number; estimatedCodingHours: number; }
export interface ActiveUser { userId: string; displayName: string; userName: string; avatarUrl?: string; activityCount: number; }
export interface TimeSeriesPoint { period: string; value: number; }
export interface LanguageUsage { language: string; projectCount: number; }
export interface DeveloperAnalytics { commits:number;pullRequests:number;reviews:number;deployments:number;projects:number;contributions:number;followers:number;posts:number;snippets:number; }
export interface ProjectAnalytics { projectId:string;name:string;isPublic:boolean;views:number;forks:number;forkingAvailable:boolean;likes:number;saves:number;contributors:number;deployments:number;activity:number; }
export interface AnalyticsDashboard {
  from: string; to: string; summary: AnalyticsSummary; activeUsers: ActiveUser[];
  projectsOverTime: TimeSeriesPoint[]; languages: LanguageUsage[];
  weeklyActivity: TimeSeriesPoint[]; monthlyActivity: TimeSeriesPoint[];
  developer:DeveloperAnalytics; projects:ProjectAnalytics[];
}
