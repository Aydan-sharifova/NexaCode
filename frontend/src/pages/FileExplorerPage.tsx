import Editor, { type OnMount } from "@monaco-editor/react";
import "../features/editor/monacoSetup";
import type { editor } from "monaco-editor";
import { useCallback, useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router-dom";
import { ConfirmDialog, Dialog } from "../components/ui/Dialog";
import { useToast } from "../contexts/ToastContext";
import { useTheme } from "../hooks/useTheme";
import { useEditorStore, type EditorTab } from "../features/editor/editorStore";
import { useAutoSave } from "../features/editor/hooks/useAutoSave";
import { useEditorTabs } from "../features/editor/hooks/useEditorTabs";
import { useKeyboardShortcuts } from "../features/editor/hooks/useKeyboardShortcuts";
import {
  getOrCreateModel,
  useMonacoConfiguration,
} from "../features/editor/hooks/useMonacoConfiguration";
import { fileExplorerApi } from "../features/fileExplorer/api";
import { FileTree } from "../features/fileExplorer/FileTree";
import {
  explorerStore,
  useExplorerSnapshot,
} from "../features/fileExplorer/store";
import type { WorkspaceNode } from "../features/fileExplorer/types";
import { VersionHistory } from "../features/fileExplorer/VersionHistory";
import { useCollaboration } from "../features/collaboration/useCollaboration";
import { PresencePanel } from "../features/collaboration/PresencePanel";
import { useCollaborationStore } from "../features/collaboration/collaborationStore";
import { useCodingSession } from "../features/analytics/useCodingSession";
import { AiAssistantPanel } from "../features/ai/AiAssistantPanel";
import { MonacoBinding } from "y-monaco";
import { useCollaborativeDocument } from "../features/collaboration/hooks/useCollaborativeDocument";
import { CollaborationStatus } from "../features/collaboration/components/CollaborationStatus";
import { RemoteUserList } from "../features/collaboration/components/RemoteUserList";
import { crdtDocumentManager } from "../features/collaboration/crdt/CrdtDocumentManager";
import { repositoryApi } from "../features/repository/api";
import { ApiError } from "../services/apiClient";
import type { FileContent } from "../features/fileExplorer/types";
import { queryKeys } from "../services/queryKeys";
import { LivePreviewPanel } from "../features/editor/LivePreviewPanel";
import { useProject } from "../features/projects/hooks";
import { VoiceCommandPanel } from "../features/voice/VoiceCommandPanel";
import type { VoiceIntent } from "../features/voice/voiceIntent";
import type { AiAction } from "../features/ai/types";
import { autonomousTestingApi } from "../features/autonomous-testing/api";
import { buildSuggestionDiff } from "../utils/suggestionDiff";

function MonacoPane({
  projectId,
  tab,
  onSelectionChange,
  readOnly,
}: {
  projectId: string;
  tab: EditorTab;
  onSelectionChange: (value: string) => void;
  readOnly: boolean;
}) {
  const { theme } = useTheme();

  const fontSize = useEditorStore((state) => state.fontSize);
  const setCursor = useEditorStore((state) => state.setCursor);
  const setViewState = useEditorStore((state) => state.setViewState);

  const remoteCursors = useCollaborationStore((state) => state.remoteCursors);

  const { configure } = useMonacoConfiguration();

  const editorRef = useRef<editor.IStandaloneCodeEditor | undefined>(undefined);

  const decorations = useRef<editor.IEditorDecorationsCollection | undefined>(
    undefined,
  );

  const bindingRef = useRef<MonacoBinding | undefined>(undefined);

  const typingTimer = useRef<number | undefined>(undefined);

  const collaborative = useCollaborativeDocument(
    projectId,
    tab.id,
    tab.content,
  );

  const mount: OnMount = (instance, monaco) => {
    editorRef.current = instance;

    const model = getOrCreateModel(
      monaco,
      tab.id,
      tab.path,
      collaborative.text.toString(),
      tab.language,
    );

    instance.setModel(model);

    bindingRef.current?.destroy();

    bindingRef.current = new MonacoBinding(
      collaborative.text,
      model,
      new Set([instance]),
      collaborative.awareness,
    );

    if (tab.viewState) {
      instance.restoreViewState(tab.viewState);
    }

    instance.focus();

    instance.onDidChangeCursorPosition((event) => {
      setCursor(tab.id, event.position.lineNumber, event.position.column);

      const selected = instance.getSelection();

      onSelectionChange(
        selected && !selected.isEmpty()
          ? (instance.getModel()?.getValueInRange(selected) ?? "")
          : "",
      );
    });

    instance.onDidChangeModelContent((event) => {
      if (event.isFlush) return;

      collaborative.awareness.setLocalStateField("typing", true);

      if (typingTimer.current) {
        window.clearTimeout(typingTimer.current);
      }

      typingTimer.current = window.setTimeout(() => {
        collaborative.awareness.setLocalStateField("typing", false);
      }, 500);
    });
  };

  useEffect(() => {
    const synchronizeStore = () =>
      useEditorStore
        .getState()
        .updateContent(tab.id, collaborative.text.toString());

    collaborative.text.observe(synchronizeStore);

    return () => collaborative.text.unobserve(synchronizeStore);
  }, [collaborative.text, tab.id]);

  useEffect(() => {
    const instance = editorRef.current;

    if (!instance) return;

    decorations.current?.clear();

    decorations.current = instance.createDecorationsCollection(
      Object.values(remoteCursors)
        .filter((cursor) => cursor.fileId === tab.id)
        .flatMap((cursor) => {
          const result: editor.IModelDeltaDecoration[] = [
            {
              range: {
                startLineNumber: cursor.lineNumber,
                startColumn: cursor.column,
                endLineNumber: cursor.lineNumber,
                endColumn: cursor.column,
              },
              options: {
                className: "remote-cursor",
              },
            },
          ];

          if (cursor.selection) {
            result.push({
              range: cursor.selection,
              options: {
                className: "remote-selection",
              },
            });
          }

          return result;
        }),
    );
  }, [remoteCursors, tab.id]);

  useEffect(
    () => () => {
      onSelectionChange("");

      if (typingTimer.current) {
        window.clearTimeout(typingTimer.current);
      }

      bindingRef.current?.destroy();
      decorations.current?.clear();

      if (editorRef.current) {
        setViewState(tab.id, editorRef.current.saveViewState());
      }
    },
    [onSelectionChange, setViewState, tab.id],
  );

  return (
    <>
      <Editor
        key={tab.id}
        height="100%"
        path={tab.path}
        language={tab.language}
        defaultValue={tab.content}
        theme={theme === "dark" ? "coding-dark" : "coding-light"}
        beforeMount={configure}
        onMount={mount}
        keepCurrentModel
        options={{
          readOnly,
          readOnlyMessage: { value: "Viewer access is read-only." },
          fontSize,
          fontLigatures: true,
          minimap: {
            enabled: true,
          },
          smoothScrolling: true,
          automaticLayout: true,
          scrollBeyondLastLine: false,
          tabSize: 2,
          wordWrap: "off",
          padding: {
            top: 12,
          },
        }}
      />

      <div className="crdt-overlay">
        <RemoteUserList awareness={collaborative.awareness} />

        <CollaborationStatus provider={collaborative.provider} />
      </div>
    </>
  );
}

export function FileExplorerPage() {
  const { projectId = "" } = useParams();

  const navigate = useNavigate();
  const { show } = useToast();
  const queryClient = useQueryClient();
  const project = useProject(projectId);
  const readOnly = project.data?.isReadOnly ?? false;

  const explorer = useExplorerSnapshot();
  const tabs = useEditorTabs();

  const { saveNow } = useAutoSave();

  useCollaboration(projectId);

  useCodingSession(projectId, tabs.activeTabId);

  const tree = useQuery({
    queryKey: queryKeys.fileTree(projectId),
    queryFn: () => fileExplorerApi.tree(projectId),
    enabled: Boolean(projectId),
  });

  const loadedTree = useRef<WorkspaceNode[] | undefined>(undefined);

  const [create, setCreate] = useState<{
    type: "file" | "folder";
    parentId?: string;
  }>();

  const [newName, setNewName] = useState("");

  const [deleting, setDeleting] = useState<WorkspaceNode>();

  const [closing, setClosing] = useState<EditorTab>();

  const [saveConflict, setSaveConflict] = useState<{
    fileId: string;
    fileName: string;
    latest: FileContent;
    closeAfterSave: boolean;
  }>();

  const [quickOpen, setQuickOpen] = useState(false);

  const [quickFilter, setQuickFilter] = useState("");

  const [historyKey, setHistoryKey] = useState(0);

  const [rightMode, setRightMode] = useState<
    "ai" | "collaboration" | "preview"
  >("ai");

  const [selectedCode, setSelectedCode] = useState("");

  const [aiSuggestion, setAiSuggestion] = useState<string>();
  const [voiceAiRequest, setVoiceAiRequest] = useState<{
    id: string;
    action: AiAction;
    message: string;
  }>();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState<string[]>([]);
  const [dragActive, setDragActive] = useState(false);
  const [leftMode, setLeftMode] = useState<"explorer" | "source">("explorer");
  const [commitMessage, setCommitMessage] = useState("");
  const [isCommitting, setIsCommitting] = useState(false);
  const [repositoryPathBusy, setRepositoryPathBusy] = useState<string>();
  const [sourceView, setSourceView] = useState<
    "changes" | "history" | "branches"
  >("changes");
  const [newBranchName, setNewBranchName] = useState("");
  const [repositoryActionBusy, setRepositoryActionBusy] = useState<string>();
  const [diffStaged, setDiffStaged] = useState(false);
  const [selectedCommitSha, setSelectedCommitSha] = useState<string>();
  const repositoryStatus = useQuery({
    queryKey: queryKeys.repository.status(projectId),
    queryFn: () => repositoryApi.status(projectId),
    enabled: Boolean(projectId),
  });
  const repositoryHistory = useQuery({
    queryKey: queryKeys.repository.history(projectId),
    queryFn: () => repositoryApi.history(projectId),
    enabled: Boolean(projectId),
  });
  const repositoryBranches = useQuery({
    queryKey: queryKeys.repository.branches(projectId),
    queryFn: () => repositoryApi.branches(projectId),
    enabled: Boolean(projectId),
  });
  const repositoryDiff = useQuery({
    queryKey: queryKeys.repository.diff(projectId, diffStaged),
    queryFn: () => repositoryApi.diff(projectId, diffStaged),
    enabled:
      Boolean(projectId) && leftMode === "source" && sourceView === "changes",
  });
  const selectedCommit = repositoryHistory.data?.find(
    (commit) => commit.sha === selectedCommitSha,
  );
  const commitDiff = useQuery({
    queryKey: queryKeys.repository.commitDiff(
      projectId,
      selectedCommitSha ?? "",
    ),
    queryFn: () => repositoryApi.commitDiff(projectId, selectedCommitSha!),
    enabled: Boolean(projectId && selectedCommitSha),
  });

  const refreshRepository = useCallback(
    async (includeWorkspaceData = false) => {
      const tasks: Promise<unknown>[] = [
        queryClient.invalidateQueries({
          queryKey: queryKeys.repository.all(projectId),
        }),
      ];
      if (includeWorkspaceData) {
        tasks.push(
          queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
          queryClient.invalidateQueries({ queryKey: queryKeys.activities }),
          queryClient.invalidateQueries({ queryKey: queryKeys.analytics }),
        );
      }
      await Promise.all(tasks);
    },
    [projectId, queryClient],
  );

  useEffect(
    () =>
      useEditorStore.subscribe((state, previous) => {
        const savedFile = state.openTabIds.some(
          (id) =>
            state.tabs[id]?.status === "Saved" &&
            previous.tabs[id]?.status === "Saving",
        );

        if (savedFile) {
          void refreshRepository();
        }
      }),
    [refreshRepository],
  );

  const commitRepository = async () => {
    const message = commitMessage.trim();
    if (!message || isCommitting) return;

    setIsCommitting(true);

    try {
      const editorState = useEditorStore.getState();
      const dirtyIds = editorState.openTabIds.filter((id) => {
        const tab = editorState.tabs[id];
        return tab && tab.content !== tab.savedContent;
      });

      await Promise.all(dirtyIds.map((id) => saveNow(id)));

      const saveDeadline = Date.now() + 7_000;
      while (Date.now() < saveDeadline) {
        const current = useEditorStore.getState();
        const savePending = current.openTabIds.some((id) => {
          const tab = current.tabs[id];
          return (
            tab && (tab.status === "Saving" || tab.content !== tab.savedContent)
          );
        });

        if (!savePending) break;
        await new Promise<void>((resolve) => window.setTimeout(resolve, 100));
      }

      const current = useEditorStore.getState();
      const unsaved = current.openTabIds.some((id) => {
        const tab = current.tabs[id];
        return tab && tab.content !== tab.savedContent;
      });

      if (unsaved) {
        throw new Error("Save the open file successfully before committing.");
      }

      const status = await repositoryApi.status(projectId);
      if (status.isClean) {
        show("There are no saved changes to commit.", "error");
        await refreshRepository();
        return;
      }

      const commit = await repositoryApi.commit(projectId, message);
      setCommitMessage("");
      await refreshRepository(true);
      setSourceView("history");
      show(`Commit ${commit.shortSha} created.`);
    } catch (error) {
      show(error instanceof Error ? error.message : "Commit failed.", "error");
    } finally {
      setIsCommitting(false);
    }
  };

  const setRepositoryStaged = async (path: string, staged: boolean) => {
    if (repositoryPathBusy) return;
    setRepositoryPathBusy(path);
    try {
      if (staged) await repositoryApi.stage(projectId, path);
      else await repositoryApi.unstage(projectId, path);
      await refreshRepository();
    } catch (error) {
      show(
        error instanceof Error
          ? error.message
          : `Could not ${staged ? "stage" : "unstage"} file.`,
        "error",
      );
    } finally {
      setRepositoryPathBusy(undefined);
    }
  };

  const createRepositoryBranch = async () => {
    const name = newBranchName.trim();
    if (!name || repositoryActionBusy) return;
    setRepositoryActionBusy("create");
    try {
      await repositoryApi.createBranch(projectId, name);
      setNewBranchName("");
      await refreshRepository();
      show(`Branch ${name} created.`);
    } catch (error) {
      show(
        error instanceof Error ? error.message : "Branch creation failed.",
        "error",
      );
    } finally {
      setRepositoryActionBusy(undefined);
    }
  };

  const checkoutRepositoryBranch = async (name: string) => {
    if (repositoryActionBusy) return;
    setRepositoryActionBusy(name);
    try {
      await repositoryApi.checkoutBranch(projectId, name);
      const [, refreshedTree] = await Promise.all([
        refreshRepository(true),
        tree.refetch(),
      ]);
      if (refreshedTree.data) explorerStore.load(refreshedTree.data);
      show(`Checked out ${name}.`);
    } catch (error) {
      show(
        error instanceof Error ? error.message : "Branch checkout failed.",
        "error",
      );
    } finally {
      setRepositoryActionBusy(undefined);
    }
  };

  const handleSelectionChange = useCallback((value: string) => {
    setSelectedCode(value);
  }, []);

  useEffect(() => {
    if (tree.data && loadedTree.current !== tree.data) {
      loadedTree.current = tree.data;
      explorerStore.load(tree.data);
    }
  }, [tree.data]);

  const reloadTree = async () => {
    const result = await tree.refetch();

    if (result.data) {
      explorerStore.load(result.data);
    }
  };

  const requestClose = useCallback((id?: string) => {
    if (!id) return;

    const tab = useEditorStore.getState().tabs[id];

    if (!tab) return;

    if (tab.content !== tab.savedContent) {
      setClosing(tab);
    } else {
      useEditorStore.getState().closeTab(id);
    }
  }, []);

  const handleSaveError = useCallback(
    async (error: unknown, fileId: string, closeAfterSave = false) => {
      if (error instanceof ApiError && error.status === 409) {
        try {
          const latest = await fileExplorerApi.content(fileId);
          const current = useEditorStore.getState().tabs[fileId];

          if (current) {
            setClosing(undefined);
            setSaveConflict({
              fileId,
              fileName: current.name,
              latest,
              closeAfterSave,
            });
            return;
          }
        } catch (reloadError) {
          show(
            reloadError instanceof Error
              ? reloadError.message
              : "Could not load the latest file version.",
            "error",
          );
          return;
        }
      }

      show(error instanceof Error ? error.message : "Save failed.", "error");
    },
    [show],
  );

  useKeyboardShortcuts({
    save: () => {
      if (!tabs.activeTabId) return;

      const fileId = tabs.activeTabId;
      void saveNow(fileId).catch(
        (error) => void handleSaveError(error, fileId),
      );
    },

    close: () => requestClose(tabs.activeTabId),

    quickOpen: () => setQuickOpen(true),
  });

  /*
   * FIX:
   * selectedId həmişə folder olmaya bilər.
   *
   * Folder seçilibsə:
   * həmin folder parent olur.
   *
   * File seçilibsə:
   * onun parent folder-i istifadə olunur.
   *
   * Heç nə seçilməyibsə:
   * parentId undefined olur və root-da yaradılır.
   */
  const selectedNode = explorer.selectedId
    ? explorer.entities.get(explorer.selectedId)
    : undefined;

  const createParentId =
    selectedNode?.nodeType === "Folder"
      ? selectedNode.id
      : selectedNode?.parentId;

  const uploadFiles = async (selected: FileList | File[]) => {
    const files = Array.from(selected);
    if (!files.length) return;
    setUploading(files.map((file) => file.name));
    try {
      await fileExplorerApi.upload(projectId, createParentId, files);
      await reloadTree();
      await refreshRepository();
      show(
        `${files.length} file${files.length === 1 ? "" : "s"} uploaded to ${selectedNode?.nodeType === "Folder" ? selectedNode.path : "/"}.`,
      );
    } catch (error) {
      show(error instanceof Error ? error.message : "Upload failed.", "error");
    } finally {
      setUploading([]);
      if (fileInputRef.current) fileInputRef.current.value = "";
      if (folderInputRef.current) folderInputRef.current.value = "";
      if (imageInputRef.current) imageInputRef.current.value = "";
    }
  };

  const uploadFolder = async (selected: FileList | File[]) => {
    const files = Array.from(selected);
    if (!files.length) return;

    setUploading(files.map((file) => file.webkitRelativePath || file.name));

    try {
      const foldersByParentAndName = new Map<string, WorkspaceNode>();
      for (const node of explorer.entities.values()) {
        if (node.nodeType !== "Folder") continue;
        foldersByParentAndName.set(
          `${node.parentId ?? "root"}\0${node.name.toLocaleLowerCase()}`,
          node,
        );
      }

      const filesByParent = new Map<string | undefined, File[]>();

      for (const file of files) {
        const relativeParts = (file.webkitRelativePath || file.name)
          .split("/")
          .filter(Boolean);
        const folderParts = relativeParts.slice(0, -1);
        let parentId = createParentId;

        for (const folderName of folderParts) {
          const key = `${parentId ?? "root"}\0${folderName.toLocaleLowerCase()}`;
          let folder = foldersByParentAndName.get(key);
          if (!folder) {
            folder = await fileExplorerApi.createFolder(
              projectId,
              parentId,
              folderName,
            );
            foldersByParentAndName.set(key, folder);
          }
          parentId = folder.id;
        }

        filesByParent.set(parentId, [
          ...(filesByParent.get(parentId) ?? []),
          file,
        ]);
      }

      for (const [parentId, parentFiles] of filesByParent) {
        for (let index = 0; index < parentFiles.length; index += 20) {
          await fileExplorerApi.upload(
            projectId,
            parentId,
            parentFiles.slice(index, index + 20),
          );
        }
      }

      await reloadTree();
      await refreshRepository();
      show(
        `Folder uploaded with ${files.length} file${files.length === 1 ? "" : "s"}.`,
      );
    } catch (error) {
      show(
        error instanceof Error ? error.message : "Folder upload failed.",
        "error",
      );
    } finally {
      setUploading([]);
      if (folderInputRef.current) folderInputRef.current.value = "";
    }
  };

  const createNode = async () => {
    if (!create || !newName.trim()) {
      return;
    }

    try {
      if (create.type === "file") {
        await fileExplorerApi.createFile(
          projectId,
          create.parentId,
          newName.trim(),
        );
      } else {
        await fileExplorerApi.createFolder(
          projectId,
          create.parentId,
          newName.trim(),
        );
      }

      const createdType = create.type;

      setCreate(undefined);
      setNewName("");

      await reloadTree();
      await refreshRepository();

      show(`${createdType === "file" ? "File" : "Folder"} created.`);
    } catch (error) {
      show(
        error instanceof Error ? error.message : "Creation failed.",
        "error",
      );
    }
  };

  const actions = {
    onOpen: (node: WorkspaceNode) =>
      void tabs
        .openFile(node)
        .catch((error) =>
          show(
            error instanceof Error ? error.message : "Unable to open file.",
            "error",
          ),
        ),

    onCreate: (type: "file" | "folder", parentId?: string) => {
      setCreate({
        type,
        parentId,
      });

      setNewName("");
    },

    onRename: async (node: WorkspaceNode, name: string) => {
      try {
        await fileExplorerApi.rename(node.id, name);

        const oldPath = node.path;
        await reloadTree();
        const renamed = explorerStore.entities.get(node.id);
        if (renamed) {
          useEditorStore
            .getState()
            .updateTabIdentity(renamed.id, renamed.name, renamed.path);
          if (node.nodeType === "Folder") {
            const prefix = `${oldPath.replace(/\/$/, "")}/`;
            Object.values(useEditorStore.getState().tabs).forEach((tab) => {
              if (tab.path.startsWith(prefix)) {
                useEditorStore
                  .getState()
                  .updateTabIdentity(
                    tab.id,
                    tab.name,
                    `${renamed.path.replace(/\/$/, "")}/${tab.path.slice(prefix.length)}`,
                  );
              }
            });
          }
        }
        await refreshRepository();
        show(`${node.nodeType} renamed.`);
      } catch (error) {
        show(
          error instanceof Error ? error.message : "Rename failed.",
          "error",
        );
      }
    },

    onDelete: setDeleting,

    onMove: async (nodeId: string, parentId?: string) => {
      if (!nodeId) return;

      try {
        await fileExplorerApi.move(nodeId, parentId);

        await reloadTree();
        await refreshRepository();
      } catch (error) {
        show(error instanceof Error ? error.message : "Move failed.", "error");
      }
    },
  };

  const resize = (side: "left" | "right", start: number) => {
    const initialLeft = tabs.leftWidth;
    const initialRight = tabs.rightWidth;

    const move = (event: MouseEvent) => {
      tabs.setPanelWidths(
        side === "left"
          ? Math.min(480, Math.max(180, initialLeft + event.clientX - start))
          : initialLeft,

        side === "right"
          ? Math.min(480, Math.max(220, initialRight - event.clientX + start))
          : initialRight,
      );
    };

    const stop = () => {
      window.removeEventListener("mousemove", move);

      window.removeEventListener("mouseup", stop);
    };

    window.addEventListener("mousemove", move);

    window.addEventListener("mouseup", stop);
  };

  const files = [...explorer.entities.values()].filter(
    (node) =>
      node.nodeType === "File" &&
      `${node.name} ${node.path}`
        .toLowerCase()
        .includes(quickFilter.toLowerCase()),
  );

  const executeVoiceIntent = async (
    intent: Exclude<VoiceIntent, { kind: "unknown" }>,
  ) => {
    if (intent.kind === "openFile") {
      const requested = intent.fileName.toLowerCase().replace(/^\//, "");
      const matches = [...explorer.entities.values()].filter(
        (node) =>
          node.nodeType === "File" &&
          (node.name.toLowerCase() === requested ||
            node.path.toLowerCase().replace(/^\//, "") === requested),
      );
      if (matches.length === 0)
        throw new Error(`File '${intent.fileName}' was not found.`);
      if (matches.length > 1)
        throw new Error(
          `More than one file is named '${intent.fileName}'. Say its full path.`,
        );
      await tabs.openFile(matches[0]);
      show(`Opened ${matches[0].path}.`);
      return;
    }
    if (intent.kind === "explain" || intent.kind === "fixError") {
      if (!tabs.activeTab)
        throw new Error("Open a file before asking AI about it.");
      setRightMode("ai");
      if (!tabs.rightPanelVisible) tabs.toggleRightPanel();
      setVoiceAiRequest({
        id: crypto.randomUUID(),
        action: intent.kind === "explain" ? "Explain" : "SuggestFix",
        message:
          intent.kind === "explain"
            ? "Explain the current selection or file."
            : "Analyze the current file and suggest a minimal fix for the visible error. Do not apply code automatically.",
      });
      return;
    }
    if (readOnly)
      throw new Error("Viewer access cannot execute code or create branches.");
    if (intent.kind === "runTests") {
      if (!tabs.activeTab)
        throw new Error("Open a runnable file or test harness first.");
      const result = await autonomousTestingApi.start(projectId, {
        workspaceNodeId: tabs.activeTab.id,
        goal: "Run bounded tests for the current file, identify failures, and report evidence without applying any code changes.",
        maximumIterations: 1,
      });
      show(`Test run ${result.status.toLowerCase()}. No fix was applied.`);
      navigate(`/projects/${projectId}/autonomous-tests`);
      return;
    }
    await repositoryApi.createBranch(projectId, intent.branchName);
    await refreshRepository();
    show(`Branch ${intent.branchName} created.`);
  };

  return (
    <main className="workspace-page">
      <header className="workspace-toolbar">
        <button onClick={() => navigate(`/projects/${projectId}/settings`)}>
          ← Project
        </button>

        <strong>Collaborative Workspace</strong>

        <div>
          <button
            onClick={() => navigate(`/projects/${projectId}/pull-requests`)}
            title="Review and merge branches"
          >
            ⇄ PRs
          </button>
          <button
            onClick={() => navigate(`/projects/${projectId}/deployments`)}
            title="Publish a versioned static deployment"
            disabled={readOnly}
          >
            ↗ Deploy
          </button>
          <VoiceCommandPanel disabled={readOnly} execute={executeVoiceIntent} />

          <button
            className={
              rightMode === "preview" && tabs.rightPanelVisible ? "active" : ""
            }
            onClick={() => {
              setRightMode("preview");
              if (!tabs.rightPanelVisible) tabs.toggleRightPanel();
            }}
            title="Run the current browser file"
            disabled={readOnly}
          >
            ▶ Run
          </button>

          <button onClick={() => setQuickOpen(true)}>⌘P</button>

          <button
            disabled={readOnly}
            onClick={() => actions.onCreate("file", createParentId)}
          >
            ＋ File
          </button>

          <button
            disabled={readOnly}
            onClick={() => actions.onCreate("folder", createParentId)}
          >
            ＋ Folder
          </button>

          <button
            disabled={readOnly}
            aria-label="Upload files"
            title="Upload files"
            onClick={() => fileInputRef.current?.click()}
          >
            ↑ File
          </button>

          <button
            disabled={readOnly}
            aria-label="Upload folder"
            title="Upload folder"
            onClick={() => folderInputRef.current?.click()}
          >
            ↑ Folder
          </button>

          <button
            disabled={readOnly}
            aria-label="Upload image"
            title="Upload image"
            onClick={() => imageInputRef.current?.click()}
          >
            ▧ Image
          </button>

          <button
            className={
              rightMode === "ai" && tabs.rightPanelVisible ? "active" : ""
            }
            onClick={() => {
              setRightMode("ai");

              if (!tabs.rightPanelVisible) {
                tabs.toggleRightPanel();
              }
            }}
          >
            ✦ AI
          </button>

          <button
            onClick={() => {
              setRightMode("collaboration");

              if (!tabs.rightPanelVisible) {
                tabs.toggleRightPanel();
              }
            }}
          >
            Collaboration
          </button>
        </div>
      </header>

      <div
        className="monaco-workspace"
        style={{
          gridTemplateColumns: `${tabs.leftWidth}px 4px minmax(280px, 1fr) ${
            tabs.rightPanelVisible ? `4px ${tabs.rightWidth}px` : ""
          }`,
        }}
      >
        <aside
          className={`explorer-panel ${dragActive ? "drop-active" : ""}`}
          onDragEnter={(event) => {
            if (event.dataTransfer.types.includes("Files")) {
              event.preventDefault();
              setDragActive(true);
            }
          }}
          onDragOver={(event) => {
            if (event.dataTransfer.types.includes("Files"))
              event.preventDefault();
          }}
          onDragLeave={(event) => {
            if (
              !event.currentTarget.contains(event.relatedTarget as Node | null)
            )
              setDragActive(false);
          }}
          onDrop={(event) => {
            if (!event.dataTransfer.files.length) return;
            event.preventDefault();
            event.stopPropagation();
            setDragActive(false);
            if (!readOnly) void uploadFiles(event.dataTransfer.files);
          }}
        >
          <header>
            <div className="workspace-left-tabs">
              <button
                className={leftMode === "explorer" ? "active" : ""}
                onClick={() => setLeftMode("explorer")}
              >
                EXPLORER
              </button>
              <button
                className={leftMode === "source" ? "active" : ""}
                onClick={() => setLeftMode("source")}
              >
                SOURCE{" "}
                {repositoryStatus.data?.files.length
                  ? `(${repositoryStatus.data.files.length})`
                  : ""}
              </button>
            </div>
            <div className="explorer-actions">
              <button
                disabled={readOnly}
                aria-label="New file"
                title="New file"
                onClick={() => actions.onCreate("file", createParentId)}
              >
                ＋
              </button>
              <button
                disabled={readOnly}
                aria-label="New folder"
                title="New folder"
                onClick={() => actions.onCreate("folder", createParentId)}
              >
                ▱
              </button>
              <button
                disabled={readOnly}
                aria-label="Upload files"
                title="Upload files"
                onClick={() => fileInputRef.current?.click()}
              >
                ↑
              </button>
              <button
                disabled={readOnly}
                aria-label="Upload folder"
                title="Upload folder"
                onClick={() => folderInputRef.current?.click()}
              >
                ⇧
              </button>
              <button
                disabled={readOnly}
                aria-label="Upload image"
                title="Upload image"
                onClick={() => imageInputRef.current?.click()}
              >
                ▧
              </button>
              <button
                aria-label="Refresh explorer"
                title="Refresh explorer"
                onClick={() => reloadTree()}
              >
                ↻
              </button>
            </div>
          </header>

          <input
            ref={fileInputRef}
            className="workspace-file-input"
            type="file"
            multiple
            onChange={(event) =>
              event.target.files && void uploadFiles(event.target.files)
            }
          />
          <input
            ref={(element) => {
              folderInputRef.current = element;
              element?.setAttribute("webkitdirectory", "");
            }}
            className="workspace-file-input"
            type="file"
            multiple
            onChange={(event) =>
              event.target.files && void uploadFolder(event.target.files)
            }
          />
          <input
            ref={imageInputRef}
            className="workspace-file-input"
            type="file"
            multiple
            accept="image/png,image/jpeg,image/webp,image/gif"
            onChange={(event) =>
              event.target.files && void uploadFiles(event.target.files)
            }
          />
          {leftMode === "explorer" && (
            <div className="upload-destination">
              Upload to:{" "}
              {selectedNode?.nodeType === "Folder" ? selectedNode.path : "/"}
            </div>
          )}
          {uploading.length > 0 && (
            <div className="upload-progress" role="status">
              <strong>Uploading…</strong>
              {uploading.map((name) => (
                <span key={name}>{name}</span>
              ))}
            </div>
          )}
          {dragActive && (
            <div className="file-drop-overlay">Drop files to upload</div>
          )}

          {leftMode === "source" ? (
            <div className="source-control-panel">
              <strong>
                ⎇ {repositoryStatus.data?.currentBranch || "main"}
              </strong>
              <nav
                className="source-view-tabs"
                aria-label="Source control views"
              >
                {(["changes", "history", "branches"] as const).map((view) => (
                  <button
                    key={view}
                    className={sourceView === view ? "active" : ""}
                    onClick={() => setSourceView(view)}
                  >
                    {view}
                  </button>
                ))}
              </nav>
              {sourceView === "changes" && (
                <>
                  <textarea
                    aria-label="Commit message"
                    placeholder="Commit message"
                    value={commitMessage}
                    onChange={(event) => setCommitMessage(event.target.value)}
                  />
                  <button
                    disabled={!commitMessage.trim() || isCommitting}
                    onClick={() => void commitRepository()}
                  >
                    {isCommitting ? "Saving & committing…" : "Commit"}
                  </button>
                  <div className="source-changes">
                    {repositoryStatus.isLoading ? (
                      <span>Loading Git status…</span>
                    ) : repositoryStatus.isError ? (
                      <span>
                        Git status unavailable.{" "}
                        <button onClick={() => void repositoryStatus.refetch()}>
                          Retry
                        </button>
                      </span>
                    ) : repositoryStatus.data?.files.length ? (
                      repositoryStatus.data.files.map((file) => {
                        const staged =
                          file.indexStatus !== " " && file.indexStatus !== "?";
                        return (
                          <div key={file.path}>
                            <code
                              title={`Index: ${file.indexStatus}; worktree: ${file.workingTreeStatus}`}
                            >
                              {file.indexStatus}
                              {file.workingTreeStatus}
                            </code>
                            <span title={file.path}>{file.path}</span>
                            <button
                              aria-label={`${staged ? "Unstage" : "Stage"} ${file.path}`}
                              title={staged ? "Unstage file" : "Stage file"}
                              disabled={Boolean(repositoryPathBusy)}
                              onClick={() =>
                                void setRepositoryStaged(file.path, !staged)
                              }
                            >
                              {repositoryPathBusy === file.path
                                ? "…"
                                : staged
                                  ? "−"
                                  : "+"}
                            </button>
                          </div>
                        );
                      })
                    ) : (
                      <span>No changes</span>
                    )}
                  </div>
                  <div className="source-diff-heading">
                    <strong>Diff</strong>
                    <label>
                      <input
                        type="checkbox"
                        checked={diffStaged}
                        onChange={(event) =>
                          setDiffStaged(event.target.checked)
                        }
                      />{" "}
                      Staged
                    </label>
                  </div>
                  {repositoryDiff.isLoading ? (
                    <span>Loading diff…</span>
                  ) : repositoryDiff.isError ? (
                    <span>Diff unavailable.</span>
                  ) : repositoryDiff.data?.patch ? (
                    <pre className="source-diff">
                      {repositoryDiff.data.patch}
                    </pre>
                  ) : (
                    <span>No diff to display.</span>
                  )}
                </>
              )}
              {sourceView === "history" && (
                <div className="source-history">
                  {repositoryHistory.isLoading ? (
                    <span>Loading history…</span>
                  ) : repositoryHistory.isError ? (
                    <span>
                      History unavailable.{" "}
                      <button onClick={() => void repositoryHistory.refetch()}>
                        Retry
                      </button>
                    </span>
                  ) : repositoryHistory.data?.length ? (
                    repositoryHistory.data.map((commit) => (
                      <button
                        className="source-history-item"
                        key={commit.sha}
                        onClick={() => setSelectedCommitSha(commit.sha)}
                      >
                        <code>{commit.shortSha}</code>
                        <span>{commit.message}</span>
                        <small>
                          {commit.authorName} ·{" "}
                          {new Date(commit.committedAt).toLocaleString()}
                        </small>
                        <i>View changes →</i>
                      </button>
                    ))
                  ) : (
                    <span>No commits yet.</span>
                  )}
                </div>
              )}
              {sourceView === "branches" && (
                <div className="source-branches">
                  <form
                    onSubmit={(event) => {
                      event.preventDefault();
                      void createRepositoryBranch();
                    }}
                  >
                    <label>
                      New branch
                      <input
                        required
                        aria-label="New branch name"
                        placeholder="e.g. feature/login"
                        value={newBranchName}
                        onChange={(event) =>
                          setNewBranchName(event.target.value)
                        }
                      />
                    </label>
                    <button
                      disabled={
                        !newBranchName.trim() || Boolean(repositoryActionBusy)
                      }
                    >
                      {repositoryActionBusy === "create"
                        ? "Creating…"
                        : "Create branch"}
                    </button>
                  </form>
                  {!newBranchName.trim() && (
                    <small>Enter a branch name to enable creation.</small>
                  )}
                  {repositoryBranches.isLoading ? (
                    <span>Loading branches…</span>
                  ) : repositoryBranches.isError ? (
                    <span>
                      Branches unavailable.{" "}
                      <button onClick={() => void repositoryBranches.refetch()}>
                        Retry
                      </button>
                    </span>
                  ) : repositoryBranches.data?.length ? (
                    repositoryBranches.data.map((branch) => (
                      <div key={branch.name}>
                        <span>
                          {branch.isCurrent ? "✓ " : ""}
                          {branch.name}
                        </span>
                        <button
                          disabled={
                            branch.isCurrent || Boolean(repositoryActionBusy)
                          }
                          onClick={() =>
                            void checkoutRepositoryBranch(branch.name)
                          }
                        >
                          {repositoryActionBusy === branch.name
                            ? "Checking out…"
                            : branch.isCurrent
                              ? "Current"
                              : "Checkout"}
                        </button>
                      </div>
                    ))
                  ) : (
                    <span>No branches found.</span>
                  )}
                </div>
              )}
            </div>
          ) : tree.isLoading ? (
            <div className="tree-empty">Loading files…</div>
          ) : (
            <FileTree {...actions} />
          )}
        </aside>

        <button
          className="panel-resizer"
          aria-label="Resize explorer"
          onMouseDown={(event) => resize("left", event.clientX)}
        />

        <section className="editor-shell">
          <div className="editor-tabs" role="tablist">
            {tabs.openTabIds.map((id) => {
              const tab = tabs.tabs[id];

              const dirty = tab.content !== tab.savedContent;

              return (
                <button
                  key={id}
                  role="tab"
                  aria-selected={id === tabs.activeTabId}
                  className={id === tabs.activeTabId ? "active" : ""}
                  onClick={() => tabs.activateTab(id)}
                >
                  <span>{tab.name}</span>

                  {dirty && <i title="Unsaved changes">●</i>}

                  <b
                    onClick={(event) => {
                      event.stopPropagation();

                      requestClose(id);
                    }}
                  >
                    ×
                  </b>
                </button>
              );
            })}
          </div>

          <div className="monaco-host">
            {tabs.activeTab?.viewer === "image" ? (
              <div className="image-viewer">
                <div className="image-viewer-toolbar">
                  <strong>{tabs.activeTab.name}</strong>
                  <button
                    onClick={() =>
                      void navigator.clipboard.writeText(tabs.activeTab!.path)
                    }
                  >
                    Copy path
                  </button>
                  <a
                    href={tabs.activeTab.objectUrl}
                    download={tabs.activeTab.name}
                  >
                    Download
                  </a>
                </div>
                <img src={tabs.activeTab.objectUrl} alt={tabs.activeTab.name} />
                <small>{tabs.activeTab.path}</small>
              </div>
            ) : tabs.activeTab ? (
              <MonacoPane
                projectId={projectId}
                tab={tabs.activeTab}
                readOnly={readOnly}
                onSelectionChange={handleSelectionChange}
              />
            ) : (
              <div className="editor-empty">
                <span className="brand-mark">C</span>

                <h2>Select a file to begin</h2>

                <p>Open a file, upload source code, or add an image.</p>
                <div className="editor-empty-actions">
                  <button
                    disabled={readOnly}
                    onClick={() => actions.onCreate("file", createParentId)}
                  >
                    New File
                  </button>
                  <button
                    disabled={readOnly}
                    onClick={() => fileInputRef.current?.click()}
                  >
                    Upload File
                  </button>
                  <button
                    disabled={readOnly}
                    onClick={() => imageInputRef.current?.click()}
                  >
                    Upload Image
                  </button>
                </div>
              </div>
            )}
          </div>

          <footer className="editor-statusbar">
            <span>{tabs.activeTab?.status ?? "Ready"}</span>

            <span>
              {tabs.activeTab
                ? `Ln ${tabs.activeTab.cursor.lineNumber}, Col ${tabs.activeTab.cursor.column}`
                : ""}
            </span>

            <span>{tabs.activeTab?.language ?? "Plain Text"}</span>

            <label>
              Font
              <input
                type="number"
                min="10"
                max="28"
                value={tabs.fontSize}
                onChange={(event) =>
                  tabs.setFontSize(Number(event.target.value))
                }
              />
            </label>
          </footer>
        </section>

        {tabs.rightPanelVisible && (
          <>
            <button
              className="panel-resizer"
              aria-label="Resize assistant panel"
              onMouseDown={(event) => resize("right", event.clientX)}
            />

            <aside className="collaboration-panel">
              {rightMode === "preview" ? (
                <LivePreviewPanel
                  projectId={projectId}
                  activeTab={tabs.activeTab}
                  tabs={tabs.openTabIds
                    .map((id) => tabs.tabs[id])
                    .filter(Boolean)}
                  disabled={readOnly}
                />
              ) : rightMode === "ai" ? (
                <AiAssistantPanel
                  projectId={projectId}
                  fileId={tabs.activeTab?.id}
                  fileName={tabs.activeTab?.name}
                  language={tabs.activeTab?.language}
                  selectedCode={selectedCode}
                  fileContent={tabs.activeTab?.content}
                  onApplySuggestion={setAiSuggestion}
                  externalRequest={voiceAiRequest}
                />
              ) : (
                <>
                  <PresencePanel />

                  {tabs.activeTab ? (
                    <VersionHistory
                      nodeId={tabs.activeTab.id}
                      refreshKey={historyKey}
                      onRestore={async () => {
                        const fresh = await fileExplorerApi.content(
                          tabs.activeTab!.id,
                        );

                        crdtDocumentManager.reset(fresh.nodeId, fresh.content);

                        tabs.acceptExternal(
                          fresh.nodeId,
                          fresh.content,
                          fresh.concurrencyToken,
                        );

                        setHistoryKey((key) => key + 1);

                        show("Version restored.");
                      }}
                    />
                  ) : (
                    <div className="tree-empty">
                      Open a file to view collaboration and history.
                    </div>
                  )}
                </>
              )}
            </aside>
          </>
        )}
      </div>

      <Dialog
        open={Boolean(create)}
        title={`New ${create?.type ?? "node"}`}
        onClose={() => setCreate(undefined)}
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() => setCreate(undefined)}
            >
              Cancel
            </button>

            <button className="ui-button primary" onClick={createNode}>
              Create
            </button>
          </>
        }
      >
        <div className="feature-form">
          <label>
            Name
            <input
              autoFocus
              value={newName}
              onChange={(event) => setNewName(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  void createNode();
                }
              }}
            />
          </label>
        </div>
      </Dialog>

      <ConfirmDialog
        open={Boolean(deleting)}
        title={`Delete ${deleting?.name ?? "node"}?`}
        description={
          deleting?.nodeType === "Folder"
            ? "This folder and every descendant will be soft-deleted."
            : "The file and history will be hidden."
        }
        destructive
        confirmLabel="Delete"
        onClose={() => setDeleting(undefined)}
        onConfirm={async () => {
          if (!deleting) return;

          try {
            await fileExplorerApi.remove(deleting.id);

            const deletedPrefix = `${deleting.path.replace(/\/$/, "")}/`;
            Object.values(useEditorStore.getState().tabs).forEach((tab) => {
              if (
                tab.id === deleting.id ||
                (deleting.nodeType === "Folder" &&
                  tab.path.startsWith(deletedPrefix))
              )
                tabs.closeTab(tab.id);
            });

            setDeleting(undefined);

            await reloadTree();
            await refreshRepository();
            show(`${deleting.nodeType} deleted.`);
          } catch (error) {
            show(
              error instanceof Error ? error.message : "Delete failed.",
              "error",
            );
          }
        }}
      />

      <Dialog
        open={Boolean(selectedCommitSha)}
        title={
          selectedCommit
            ? `${selectedCommit.shortSha} · ${selectedCommit.message}`
            : "Commit changes"
        }
        description={
          selectedCommit
            ? `${selectedCommit.authorName} · ${new Date(selectedCommit.committedAt).toLocaleString()}`
            : "Loading commit…"
        }
        onClose={() => setSelectedCommitSha(undefined)}
      >
        <div className="commit-detail">
          {commitDiff.isLoading ? (
            <span role="status">Loading commit changes…</span>
          ) : commitDiff.isError ? (
            <div>
              <p>{commitDiff.error.message}</p>
              <button onClick={() => void commitDiff.refetch()}>Retry</button>
            </div>
          ) : commitDiff.data?.patch ? (
            <pre>{commitDiff.data.patch}</pre>
          ) : (
            <p>This commit contains no file changes.</p>
          )}
        </div>
      </Dialog>

      <Dialog
        open={Boolean(aiSuggestion)}
        title="Apply AI suggestion?"
        description="This replaces the current editor content. Review the suggestion first; the file will not be saved automatically."
        onClose={() => setAiSuggestion(undefined)}
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() => setAiSuggestion(undefined)}
            >
              Cancel
            </button>
            <button
              className="ui-button primary"
              onClick={() => {
                if (aiSuggestion && tabs.activeTabId)
                  crdtDocumentManager.reset(tabs.activeTabId, aiSuggestion);
                setAiSuggestion(undefined);
              }}
            >
              Apply to editor
            </button>
          </>
        }
      >
        {aiSuggestion &&
          (() => {
            const diff = buildSuggestionDiff(
              tabs.activeTab?.content ?? "",
              aiSuggestion,
            );
            return (
              <div
                className="ai-suggestion-diff"
                aria-label="AI suggestion diff"
              >
                <header>
                  <span>− Current</span>
                  <span>+ Suggested</span>
                </header>
                <pre>
                  {diff.lines.map((line, index) => (
                    <code className={line.kind} key={`${index}-${line.text}`}>
                      {line.kind === "removed"
                        ? "−"
                        : line.kind === "added"
                          ? "+"
                          : " "}{" "}
                      {line.text || " "}
                    </code>
                  ))}
                </pre>
                {diff.truncated && (
                  <small>Preview limited to the first 400 diff lines.</small>
                )}
              </div>
            );
          })()}
      </Dialog>

      <Dialog
        open={Boolean(closing)}
        title={`Save changes to ${closing?.name ?? ""}?`}
        description="Your changes will be lost if you close without saving."
        onClose={() => setClosing(undefined)}
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() => setClosing(undefined)}
            >
              Cancel
            </button>

            <button
              className="ui-button danger"
              onClick={() => {
                if (closing) {
                  tabs.discardChanges(closing.id);

                  tabs.closeTab(closing.id);
                }

                setClosing(undefined);
              }}
            >
              Don't Save
            </button>

            <button
              className="ui-button primary"
              onClick={async () => {
                if (!closing) {
                  return;
                }

                try {
                  await saveNow(closing.id);

                  tabs.closeTab(closing.id);

                  setClosing(undefined);
                } catch (error) {
                  await handleSaveError(error, closing.id, true);
                }
              }}
            >
              Save
            </button>
          </>
        }
      >
        <div className="confirmation-note">
          The server concurrency token will be checked before saving.
        </div>
      </Dialog>

      <Dialog
        open={Boolean(saveConflict)}
        title={`Newer version of ${saveConflict?.fileName ?? "this file"} found`}
        description="Another client saved this file after you opened it. Your local edits are still preserved."
        onClose={() => setSaveConflict(undefined)}
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() => setSaveConflict(undefined)}
            >
              Keep editing
            </button>

            <button
              className="ui-button ghost"
              onClick={() => {
                if (!saveConflict) return;

                crdtDocumentManager.reset(
                  saveConflict.fileId,
                  saveConflict.latest.content,
                );
                tabs.acceptExternal(
                  saveConflict.fileId,
                  saveConflict.latest.content,
                  saveConflict.latest.concurrencyToken,
                );

                if (saveConflict.closeAfterSave) {
                  tabs.closeTab(saveConflict.fileId);
                }

                setSaveConflict(undefined);
                show("Latest server version loaded.");
              }}
            >
              Load server version
            </button>

            <button
              className="ui-button primary"
              onClick={async () => {
                if (!saveConflict) return;

                const { fileId, latest, closeAfterSave } = saveConflict;
                tabs.rebaseLocalChanges(
                  fileId,
                  latest.content,
                  latest.concurrencyToken,
                );

                try {
                  await saveNow(fileId);

                  if (closeAfterSave) {
                    tabs.closeTab(fileId);
                  }

                  setSaveConflict(undefined);
                  show("Local changes saved over the newer version.");
                } catch (error) {
                  await handleSaveError(error, fileId, closeAfterSave);
                }
              }}
            >
              Save my version
            </button>
          </>
        }
      >
        <div className="confirmation-note">
          Choose “Load server version” to discard your local edits, or “Save my
          version” to explicitly replace the latest server content.
        </div>
      </Dialog>

      <Dialog
        open={quickOpen}
        title="Quick Open"
        onClose={() => setQuickOpen(false)}
      >
        <div className="quick-open">
          <input
            autoFocus
            value={quickFilter}
            onChange={(event) => setQuickFilter(event.target.value)}
            placeholder="Search files by name or path…"
          />

          {files.slice(0, 20).map((node) => (
            <button
              key={node.id}
              onClick={() => {
                void tabs.openFile(node);

                setQuickOpen(false);

                setQuickFilter("");
              }}
            >
              <strong>{node.name}</strong>

              <small>{node.path}</small>
            </button>
          ))}
        </div>
      </Dialog>
    </main>
  );
}
