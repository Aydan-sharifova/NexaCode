import { apiClient } from "../../services/apiClient";
import type { FileContent, FileVersion, FileVersionDetails, VersionComparison, WorkspaceNode } from "./types";
export const fileExplorerApi = {
  tree: (projectId: string) => apiClient.get<WorkspaceNode[]>(`/projects/${projectId}/nodes`),
  createFolder: (projectId: string, parentId: string | undefined, name: string) => apiClient.post<WorkspaceNode>(`/projects/${projectId}/folders`, { parentId, name }),
  createFile: (projectId: string, parentId: string | undefined, name: string) => apiClient.post<WorkspaceNode>(`/projects/${projectId}/files`, { parentId, name, content: "" }),
  rename: (id: string, name: string) => apiClient.put<WorkspaceNode>(`/nodes/${id}/name`, { name }),
  move: (id: string, parentId?: string) => apiClient.put<WorkspaceNode>(`/nodes/${id}/parent`, { parentId }),
  remove: (id: string) => apiClient.delete<void>(`/nodes/${id}`),
  content: (id: string) => apiClient.get<FileContent>(`/files/${id}/content`),
  save: (id: string, content: string, concurrencyToken: string) => apiClient.put<FileContent>(`/files/${id}/content`, { content, concurrencyToken }),
  versions: (id: string) => apiClient.get<FileVersion[]>(`/files/${id}/versions`),
  version: (nodeId: string, versionId: string) => apiClient.get<FileVersionDetails>(`/files/${nodeId}/versions/${versionId}`),
  compare: (nodeId: string, leftId: string, rightId: string) => apiClient.get<VersionComparison>(`/files/${nodeId}/versions/compare?leftId=${leftId}&rightId=${rightId}`),
  restoreVersion: (nodeId: string, versionId: string) => apiClient.post<FileContent>(`/files/${nodeId}/versions/${versionId}/restore`),
  upload: (projectId: string, parentId: string | undefined, files: File[]) => {
    const form = new FormData();
    if (parentId) form.append("parentId", parentId);
    files.forEach((file) => form.append("files", file, file.name));
    return apiClient.postForm<WorkspaceNode[]>(`/projects/${projectId}/files/upload`, form);
  },
  raw: (nodeId: string) => apiClient.getBlob(`/files/${nodeId}/raw`),
};
