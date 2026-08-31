import { useEffect } from "react";
import { useEditorStore } from "../editor/editorStore";
import { fileExplorerApi } from "../fileExplorer/api";
import { useCollaborationStore } from "./collaborationStore";
import { signalRService } from "./signalRService";
import type { CodeOperation, TextRange } from "./types";

function offsetAt(content: string, line: number, column: number) {
  const lines = content.split("\n"); let offset = 0;
  for (let index = 0; index < line - 1; index += 1) offset += (lines[index]?.length ?? 0) + 1;
  return offset + column - 1;
}
function applyOperation(content: string, range: TextRange, insertedText: string) {
  const start = offsetAt(content, range.startLineNumber, range.startColumn);
  const end = offsetAt(content, range.endLineNumber, range.endColumn);
  return content.slice(0, start) + insertedText + content.slice(end);
}

export function useCollaboration(projectId: string, activeFileId?: string) {
  const connectionState = useCollaborationStore((state) => state.connectionState);
  useEffect(() => {
    if (!projectId) return;
    void signalRService.joinProject(projectId).catch(() => undefined);
    return () => { void signalRService.leaveProject(projectId); };
  }, [projectId]);
  useEffect(() => {
    if (!activeFileId) return;
    const tab = useEditorStore.getState().tabs[activeFileId]; if (!tab) return;
    void fileExplorerApi.content(activeFileId).then((content) => signalRService.joinFile(activeFileId, content.versionNumber));
    return () => { void signalRService.leaveFile(activeFileId); };
  }, [activeFileId]);
  useEffect(() => signalRService.onOperation((operation: CodeOperation) => {
    const tab = useEditorStore.getState().tabs[operation.fileId]; if (!tab) return;
    useEditorStore.getState().applyRemoteContent(operation.fileId, applyOperation(tab.content, operation.range, operation.insertedText));
  }), []);
  useEffect(() => {
    const resync = async (fileId: string) => {
      const current = useEditorStore.getState().tabs[fileId]; if (!current) return;
      if (current.content !== current.savedContent && !current.suppressAutoSave) {
        useEditorStore.getState().markSaveError(fileId);
        return;
      }
      const fresh = await fileExplorerApi.content(fileId);
      useEditorStore.getState().acceptExternal(fileId, fresh.content, fresh.concurrencyToken);
      useCollaborationStore.getState().setLiveVersion(fileId, fresh.versionNumber);
    };
    const offChanged = signalRService.onFileChanged((message) => void resync(message.fileId));
    const offResync = signalRService.onResync((message) => void resync(message.fileId));
    return () => { offChanged(); offResync(); };
  }, []);
  return { connectionState };
}
