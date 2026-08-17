import { useCallback, useEffect, useRef } from "react";
import { fileExplorerApi } from "../../fileExplorer/api";
import { useEditorStore } from "../editorStore";
import { signalRService } from "../../collaboration/signalRService";
import { ApiError } from "../../../services/apiClient";

export function useAutoSave(delay = 1500) {
  const timers = useRef(new Map<string, number>()); const inFlight = useRef(new Set<string>());
  const saveNow = useCallback(async (id: string) => {
    const state = useEditorStore.getState(); const tab = state.tabs[id];
    if (!tab || tab.content === tab.savedContent || inFlight.current.has(id)) return;
    const content = tab.content; const token = tab.concurrencyToken; const requestVersion = tab.requestVersion;
    inFlight.current.add(id); state.markSaving(id, requestVersion);
    try { const result = await fileExplorerApi.save(id, content, token); useEditorStore.getState().acknowledgeSave(id, requestVersion, content, result.concurrencyToken); signalRService.notifyFileChanged(id, result.versionNumber, result.concurrencyToken); }
    catch (error) {
      if (error instanceof ApiError && error.status === 409) useEditorStore.getState().markSaveConflict(id);
      else useEditorStore.getState().markSaveError(id, !navigator.onLine);
      throw error;
    }
    finally { inFlight.current.delete(id); const current = useEditorStore.getState().tabs[id]; if (current && current.content !== current.savedContent && !current.suppressAutoSave) timers.current.set(id, window.setTimeout(() => void saveNow(id).catch(() => undefined), delay)); }
  }, [delay]);
  useEffect(() => useEditorStore.subscribe((state, previous) => {
    for (const id of state.openTabIds) { const tab = state.tabs[id]; const old = previous.tabs[id]; if (tab?.content !== old?.content && tab.content !== tab.savedContent && !tab.suppressAutoSave) { const existing = timers.current.get(id); if (existing) clearTimeout(existing); timers.current.set(id, window.setTimeout(() => void saveNow(id).catch(() => undefined), delay)); } }
  }), [delay, saveNow]);
  useEffect(() => () => timers.current.forEach(clearTimeout), []);
  return { saveNow, isSaving: (id: string) => inFlight.current.has(id) };
}
