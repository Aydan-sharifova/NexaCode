import { apiClient } from "../../services/apiClient";

export type PlanStatus = "Draft" | "Approved" | "Applied" | "Rejected";
export type Priority = "Low" | "Medium" | "High" | "Critical";
export interface PlanTask { title: string; description: string; priority: Priority; }
export interface PlanIssue { title: string; description: string; priority: Priority; tasks: PlanTask[]; }
export interface PlanMilestone { title: string; description: string; issues: PlanIssue[]; }
export interface PlanBlueprint { title: string; summary: string; defaultLanguage: string; sections: { architecture: string; database: string; api: string; frontend: string; authentication: string; testing: string; deployment: string; }; milestones: PlanMilestone[]; }
export interface ProjectPlanSummary { id: string; title: string; summary: string; defaultLanguage: string; status: PlanStatus; version: number; createdAt: string; createdProjectId?: string; }
export interface ProjectPlanDetails { id: string; idea: string; plan: PlanBlueprint; status: PlanStatus; version: number; provider: string; model: string; createdAt: string; updatedAt: string; approvedAt?: string; appliedAt?: string; createdProjectId?: string; }
export const plannerKeys = { all: ["project-plans"] as const, detail: (id: string) => ["project-plans", id] as const };
export const projectPlannerApi = {
  list: () => apiClient.get<ProjectPlanSummary[]>("/project-plans"),
  get: (id: string) => apiClient.get<ProjectPlanDetails>(`/project-plans/${id}`),
  generate: (idea: string) => apiClient.post<ProjectPlanDetails>("/project-plans", { idea }),
  approve: (id: string, expectedVersion: number) => apiClient.post<ProjectPlanDetails>(`/project-plans/${id}/approve`, { expectedVersion }),
  reject: (id: string, expectedVersion: number) => apiClient.post<ProjectPlanDetails>(`/project-plans/${id}/reject`, { expectedVersion }),
  apply: (id: string, expectedVersion: number) => apiClient.post<string>(`/project-plans/${id}/apply`, { expectedVersion, confirmBulkCreation: true }),
};
