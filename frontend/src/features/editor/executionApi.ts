import { apiClient } from "../../services/apiClient";

export type CodeExecutionResult = {
  exitCode: number | null;
  stdout: string;
  stderr: string;
  timedOut: boolean;
  durationMs: number;
  debuggingIncidentId: string | null;
};

export const executionApi = {
  runCSharp: (projectId: string, source: string, workspaceNodeId?: string) =>
    apiClient.post<CodeExecutionResult>(`/projects/${projectId}/execution/csharp`, { source, timeoutSeconds: 8, workspaceNodeId }),
};
