import { useQueryClient } from "@tanstack/react-query";
import { fileExplorerApi } from "../../fileExplorer/api";
import type { WorkspaceNode } from "../../fileExplorer/types";
import { detectLanguage } from "../languages";
import { useEditorStore } from "../editorStore";

export function useEditorTabs() {
  const queryClient = useQueryClient();
  const tabs = useEditorStore((state) => state.tabs);
  const openTabIds = useEditorStore((state) => state.openTabIds);
  const activeTabId = useEditorStore((state) => state.activeTabId);
  const closedTabHistory = useEditorStore((state) => state.closedTabHistory);
  const fontSize = useEditorStore((state) => state.fontSize);
  const leftWidth = useEditorStore((state) => state.leftWidth);
  const rightWidth = useEditorStore((state) => state.rightWidth);
  const rightPanelVisible = useEditorStore((state) => state.rightPanelVisible);
  const activateTab = useEditorStore((state) => state.activateTab);
  const openTab = useEditorStore((state) => state.openTab);
  const closeStoreTab = useEditorStore((state) => state.closeTab);
  const discardChanges = useEditorStore((state) => state.discardChanges);
  const acceptExternal = useEditorStore((state) => state.acceptExternal);
  const rebaseLocalChanges = useEditorStore((state) => state.rebaseLocalChanges);
  const setFontSize = useEditorStore((state) => state.setFontSize);
  const setPanelWidths = useEditorStore((state) => state.setPanelWidths);
  const toggleRightPanel = useEditorStore((state) => state.toggleRightPanel);
  const openFile = async (node: WorkspaceNode) => {
    if (useEditorStore.getState().tabs[node.id]) { activateTab(node.id); return; }
    if (/\.(png|jpe?g|webp|gif|svg)$/i.test(node.name)) {
      const blob = await fileExplorerApi.raw(node.id);
      openTab({ id: node.id, name: node.name, path: node.path, language: "plaintext", content: "", savedContent: "", concurrencyToken: "", viewer: "image", objectUrl: URL.createObjectURL(blob) });
      return;
    }
    const file = await queryClient.fetchQuery({ queryKey: ["file-content", node.id], queryFn: () => fileExplorerApi.content(node.id) });
    openTab({ id: node.id, name: node.name, path: file.path, language: detectLanguage(node.name), content: file.content, savedContent: file.content, concurrencyToken: file.concurrencyToken });
  };
  const closeTab = (id: string) => { const url = useEditorStore.getState().tabs[id]?.objectUrl; if (url) URL.revokeObjectURL(url); closeStoreTab(id); };
  return { tabs, openTabIds, activeTabId, closedTabHistory, fontSize, leftWidth, rightWidth, rightPanelVisible, activateTab, closeTab, discardChanges, acceptExternal, rebaseLocalChanges, setFontSize, setPanelWidths, toggleRightPanel, openFile, activeTab: activeTabId ? tabs[activeTabId] : undefined };
}
