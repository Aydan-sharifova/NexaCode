import { apiClient } from "../../services/apiClient";
import type { PullRequestComment, PullRequestDetails, PullRequestDiff, PullRequestListItem, PullRequestPolicy, PullRequestStatus, ReviewDecision } from "./types";

const root = (projectId: string) => `/projects/${projectId}/pull-requests`;
export const pullRequestApi = {
  list: (projectId: string, status?: PullRequestStatus) => apiClient.get<PullRequestListItem[]>(`${root(projectId)}${status ? `?status=${status}` : ""}`),
  get: (projectId: string, number: number) => apiClient.get<PullRequestDetails>(`${root(projectId)}/${number}`),
  diff: (projectId: string, number: number) => apiClient.get<PullRequestDiff>(`${root(projectId)}/${number}/diff`),
  create: (projectId: string, input: { title: string; description?: string; sourceBranch: string; targetBranch?: string }) => apiClient.post<PullRequestDetails>(root(projectId), input),
  review: (projectId: string, number: number, decision: ReviewDecision, body?: string) => apiClient.put<PullRequestDetails>(`${root(projectId)}/${number}/review`, { decision, body }),
  comment: (projectId: string, number: number, input: { body: string; filePath?: string; lineNumber?: number; isBlocking: boolean }) => apiClient.post<PullRequestComment>(`${root(projectId)}/${number}/comments`, input),
  resolve: (projectId: string, number: number, commentId: string) => apiClient.put<PullRequestComment>(`${root(projectId)}/${number}/comments/${commentId}/resolve`, {}),
  refresh: (projectId: string, number: number) => apiClient.post<PullRequestDetails>(`${root(projectId)}/${number}/refresh`),
  merge: (projectId: string, number: number) => apiClient.post<PullRequestDetails>(`${root(projectId)}/${number}/merge`),
  close: (projectId: string, number: number) => apiClient.post<PullRequestDetails>(`${root(projectId)}/${number}/close`),
  tests: (projectId: string, number: number, passed: boolean, summary?: string) => apiClient.put<PullRequestDetails>(`${root(projectId)}/${number}/tests`, { passed, summary }),
  policy: (projectId: string) => apiClient.get<PullRequestPolicy>(`${root(projectId)}/policy`),
  configurePolicy: (projectId: string, policy: PullRequestPolicy) => apiClient.put<PullRequestPolicy>(`${root(projectId)}/policy`, policy),
};
