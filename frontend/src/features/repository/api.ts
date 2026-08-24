import { apiClient } from "../../services/apiClient";

export interface GitFileStatus { path: string; indexStatus: string; workingTreeStatus: string }
export interface GitStatus { currentBranch: string; isClean: boolean; files: GitFileStatus[] }
export interface GitCommit { sha: string; shortSha: string; authorName: string; message: string; committedAt: string }
export interface GitBranch { name: string; isCurrent: boolean }
export interface GitDiff { patch: string }

export const repositoryApi = {
  status: (projectId: string) => apiClient.get<GitStatus>(`/projects/${projectId}/repository/status`),
  commit: (projectId: string, message: string) => apiClient.post<GitCommit>(`/projects/${projectId}/repository/commits`, { message }),
  stage: (projectId: string, path: string) => apiClient.post<void>(`/projects/${projectId}/repository/stage`, { path }),
  unstage: (projectId: string, path: string) => apiClient.post<void>(`/projects/${projectId}/repository/unstage`, { path }),
  history: (projectId: string, take = 30) => apiClient.get<GitCommit[]>(`/projects/${projectId}/repository/commits?take=${take}`),
  branches: (projectId: string) => apiClient.get<GitBranch[]>(`/projects/${projectId}/repository/branches`),
  createBranch: (projectId: string, name: string) => apiClient.post<void>(`/projects/${projectId}/repository/branches`, { name }),
  checkoutBranch: (projectId: string, name: string) => apiClient.post<void>(`/projects/${projectId}/repository/branches/checkout`, { name }),
  diff: (projectId: string, staged = false) => apiClient.get<GitDiff>(`/projects/${projectId}/repository/diff?staged=${staged}`),
  commitDiff: (projectId: string, sha: string) => apiClient.get<GitDiff>(`/projects/${projectId}/repository/commits/${encodeURIComponent(sha)}/diff`),
};
