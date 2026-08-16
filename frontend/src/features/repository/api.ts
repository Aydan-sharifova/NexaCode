import { apiClient } from "../../services/apiClient";

export interface GitFileStatus { path: string; indexStatus: string; workingTreeStatus: string }
export interface GitStatus { currentBranch: string; isClean: boolean; files: GitFileStatus[] }
export interface GitCommit { sha: string; shortSha: string; authorName: string; message: string; committedAt: string }

export const repositoryApi = {
  status: (projectId: string) => apiClient.get<GitStatus>(`/projects/${projectId}/repository/status`),
  commit: (projectId: string, message: string) => apiClient.post<GitCommit>(`/projects/${projectId}/repository/commits`, { message }),
};
