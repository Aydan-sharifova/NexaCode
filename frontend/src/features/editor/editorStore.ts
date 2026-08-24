import type { editor } from "monaco-editor";
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { EditorLanguage } from "./languages";

export type SaveStatus = "Saved" | "Saving" | "Unsaved" | "Error" | "Offline";
export interface EditorTab { id: string; name: string; path: string; language: EditorLanguage; content: string; savedContent: string; concurrencyToken: string; viewer?: "image"; objectUrl?: string; status: SaveStatus; requestVersion: number; acknowledgedVersion: number; suppressAutoSave: boolean; viewState?: editor.ICodeEditorViewState | null; cursor: { lineNumber: number; column: number }; }
interface EditorState {
  tabs: Record<string, EditorTab>; openTabIds: string[]; activeTabId?: string; closedTabHistory: string[]; fontSize: number; leftWidth: number; rightWidth: number; rightPanelVisible: boolean;
  openTab: (tab: Omit<EditorTab, "status" | "requestVersion" | "acknowledgedVersion" | "suppressAutoSave" | "cursor">) => void; activateTab: (id: string) => void; updateContent: (id: string, content: string) => void; updateTabIdentity: (id: string, name: string, path: string) => void; applyRemoteContent: (id: string, content: string) => void; acceptExternal: (id: string, content: string, token: string) => void; rebaseLocalChanges: (id: string, serverContent: string, token: string) => void; markSaving: (id: string, requestVersion: number) => void; acknowledgeSave: (id: string, requestVersion: number, content: string, token: string) => void; markSaveError: (id: string, offline?: boolean) => void; markSaveConflict: (id: string) => void; closeTab: (id: string) => void; discardChanges: (id: string) => void; setViewState: (id: string, viewState: editor.ICodeEditorViewState | null) => void; setCursor: (id: string, lineNumber: number, column: number) => void; setFontSize: (fontSize: number) => void; setPanelWidths: (left: number, right: number) => void; toggleRightPanel: () => void;
}

export const useEditorStore = create<EditorState>()(persist((set, get) => ({
  tabs: {}, openTabIds: [], closedTabHistory: [], fontSize: 14, leftWidth: 272, rightWidth: 360, rightPanelVisible: true,
  openTab: (input) => set((state) => state.tabs[input.id] ? { activeTabId: input.id } : { tabs: { ...state.tabs, [input.id]: { ...input, status: "Saved", requestVersion: 0, acknowledgedVersion: 0, suppressAutoSave: false, cursor: { lineNumber: 1, column: 1 } } }, openTabIds: [...state.openTabIds, input.id], activeTabId: input.id }),
  activateTab: (id) => { if (get().tabs[id]) set({ activeTabId: id }); },
  updateContent: (id, content) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], content, status: content === state.tabs[id].savedContent ? "Saved" : "Unsaved", suppressAutoSave: false, requestVersion: state.tabs[id].requestVersion + 1 } } })),
  updateTabIdentity: (id, name, path) => set((state) => state.tabs[id] ? ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], name, path } } }) : state),
  applyRemoteContent: (id, content) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], content, status: "Unsaved", suppressAutoSave: true, requestVersion: state.tabs[id].requestVersion + 1 } } })),
  acceptExternal: (id, content, token) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], content, savedContent: content, concurrencyToken: token, status: "Saved", suppressAutoSave: false, requestVersion: state.tabs[id].requestVersion + 1 } } })),
  rebaseLocalChanges: (id, serverContent, token) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], savedContent: serverContent, concurrencyToken: token, status: "Unsaved", suppressAutoSave: false, requestVersion: state.tabs[id].requestVersion + 1 } } })),
  markSaving: (id, requestVersion) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], status: "Saving", requestVersion } } })),
  acknowledgeSave: (id, requestVersion, content, token) => set((state) => { const tab = state.tabs[id]; if (!tab || requestVersion < tab.acknowledgedVersion) return state; const stillCurrent = tab.content === content; return { tabs: { ...state.tabs, [id]: { ...tab, savedContent: content, concurrencyToken: token, acknowledgedVersion: requestVersion, status: stillCurrent ? "Saved" : "Unsaved" } } }; }),
  markSaveError: (id, offline = false) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], status: offline ? "Offline" : "Error" } } })),
  markSaveConflict: (id) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], status: "Error", suppressAutoSave: true } } })),
  closeTab: (id) => set((state) => { const index = state.openTabIds.indexOf(id); const openTabIds = state.openTabIds.filter((item) => item !== id); const tabs = { ...state.tabs }; delete tabs[id]; return { tabs, openTabIds, activeTabId: state.activeTabId === id ? openTabIds[Math.min(index, openTabIds.length - 1)] : state.activeTabId, closedTabHistory: [id, ...state.closedTabHistory.filter((item) => item !== id)].slice(0, 20) }; }),
  discardChanges: (id) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], content: state.tabs[id].savedContent, status: "Saved" } } })),
  setViewState: (id, viewState) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], viewState } } })), setCursor: (id, lineNumber, column) => set((state) => ({ tabs: { ...state.tabs, [id]: { ...state.tabs[id], cursor: { lineNumber, column } } } })),
  setFontSize: (fontSize) => set({ fontSize: Math.min(28, Math.max(10, fontSize)) }), setPanelWidths: (leftWidth, rightWidth) => set({ leftWidth, rightWidth }), toggleRightPanel: () => set((state) => ({ rightPanelVisible: !state.rightPanelVisible })),
}), {
  name: "coding-editor-preferences",
  partialize: (state) => ({
    fontSize: state.fontSize,
    leftWidth: state.leftWidth,
    rightWidth: state.rightWidth,
    rightPanelVisible: state.rightPanelVisible,
  }),
  merge: (persisted, current) => {
    const preferences = persisted as Partial<EditorState>;
    return {
      ...current,
      ...preferences,
      rightWidth: Math.max(320, preferences.rightWidth ?? current.rightWidth),
    };
  },
}));
