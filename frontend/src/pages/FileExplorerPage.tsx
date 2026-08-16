import Editor, { type OnMount } from "@monaco-editor/react";
import "../features/editor/monacoSetup";
import type { editor } from "monaco-editor";
import { useCallback, useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
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

function MonacoPane({
  projectId,
  tab,
  onSelectionChange,
}: {
  projectId: string;
  tab: EditorTab;
  onSelectionChange: (value: string) => void;
}) {
  const { theme } = useTheme();

  const fontSize = useEditorStore((state) => state.fontSize);
  const setCursor = useEditorStore((state) => state.setCursor);
  const setViewState = useEditorStore((state) => state.setViewState);

  const remoteCursors = useCollaborationStore(
    (state) => state.remoteCursors
  );

  const { configure } = useMonacoConfiguration();

  const editorRef =
    useRef<editor.IStandaloneCodeEditor | undefined>(undefined);

  const decorations =
    useRef<editor.IEditorDecorationsCollection | undefined>(undefined);

  const bindingRef =
    useRef<MonacoBinding | undefined>(undefined);

  const typingTimer =
    useRef<number | undefined>(undefined);

  const collaborative = useCollaborativeDocument(
    projectId,
    tab.id,
    tab.content
  );

  const mount: OnMount = (instance, monaco) => {
    editorRef.current = instance;

    const model = getOrCreateModel(
      monaco,
      tab.id,
      tab.path,
      collaborative.text.toString(),
      tab.language
    );

    instance.setModel(model);

    bindingRef.current?.destroy();

    bindingRef.current = new MonacoBinding(
      collaborative.text,
      model,
      new Set([instance]),
      collaborative.awareness
    );

    if (tab.viewState) {
      instance.restoreViewState(tab.viewState);
    }

    instance.focus();

    instance.onDidChangeCursorPosition((event) => {
      setCursor(
        tab.id,
        event.position.lineNumber,
        event.position.column
      );

      const selected = instance.getSelection();

      onSelectionChange(
        selected && !selected.isEmpty()
          ? instance.getModel()?.getValueInRange(selected) ?? ""
          : ""
      );
    });

    instance.onDidChangeModelContent((event) => {
      if (event.isFlush) return;

      collaborative.awareness.setLocalStateField(
        "typing",
        true
      );

      if (typingTimer.current) {
        window.clearTimeout(typingTimer.current);
      }

      typingTimer.current = window.setTimeout(() => {
        collaborative.awareness.setLocalStateField(
          "typing",
          false
        );
      }, 500);
    });
  };

  useEffect(() => {
    const synchronizeStore = () =>
      useEditorStore
        .getState()
        .updateContent(
          tab.id,
          collaborative.text.toString()
        );

    collaborative.text.observe(synchronizeStore);

    return () =>
      collaborative.text.unobserve(synchronizeStore);
  }, [collaborative.text, tab.id]);

  useEffect(() => {
    const instance = editorRef.current;

    if (!instance) return;

    decorations.current?.clear();

    decorations.current =
      instance.createDecorationsCollection(
        Object.values(remoteCursors)
          .filter(
            (cursor) => cursor.fileId === tab.id
          )
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
          })
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
        setViewState(
          tab.id,
          editorRef.current.saveViewState()
        );
      }
    },
    [onSelectionChange, setViewState, tab.id]
  );

  return (
    <>
      <Editor
        key={tab.id}
        height="100%"
        path={tab.path}
        language={tab.language}
        defaultValue={tab.content}
        theme={
          theme === "dark"
            ? "coding-dark"
            : "coding-light"
        }
        beforeMount={configure}
        onMount={mount}
        keepCurrentModel
        options={{
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
        <RemoteUserList
          awareness={collaborative.awareness}
        />

        <CollaborationStatus
          provider={collaborative.provider}
        />
      </div>
    </>
  );
}

export function FileExplorerPage() {
  const { projectId = "" } = useParams();

  const navigate = useNavigate();
  const { show } = useToast();

  const explorer = useExplorerSnapshot();
  const tabs = useEditorTabs();

  const { saveNow } = useAutoSave();

  useCollaboration(projectId);

  useCodingSession(
    projectId,
    tabs.activeTabId
  );

  const tree = useQuery({
    queryKey: ["file-tree", projectId],
    queryFn: () =>
      fileExplorerApi.tree(projectId),
    enabled: Boolean(projectId),
  });

  const loadedTree =
    useRef<WorkspaceNode[] | undefined>(undefined);

  const [create, setCreate] = useState<{
    type: "file" | "folder";
    parentId?: string;
  }>();

  const [newName, setNewName] = useState("");

  const [deleting, setDeleting] =
    useState<WorkspaceNode>();

  const [closing, setClosing] =
    useState<EditorTab>();

  const [quickOpen, setQuickOpen] =
    useState(false);

  const [quickFilter, setQuickFilter] =
    useState("");

  const [historyKey, setHistoryKey] =
    useState(0);

  const [rightMode, setRightMode] =
    useState<"ai" | "collaboration">("ai");

  const [selectedCode, setSelectedCode] =
    useState("");

  const [aiSuggestion, setAiSuggestion] =
    useState<string>();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState<string[]>([]);
  const [dragActive, setDragActive] = useState(false);
  const [leftMode, setLeftMode] = useState<"explorer" | "source">("explorer");
  const [commitMessage, setCommitMessage] = useState("");
  const repositoryStatus = useQuery({ queryKey: ["repository-status", projectId], queryFn: () => repositoryApi.status(projectId), enabled: Boolean(projectId) });

  const handleSelectionChange = useCallback(
    (value: string) => {
      setSelectedCode(value);
    },
    []
  );

  useEffect(() => {
    if (
      tree.data &&
      loadedTree.current !== tree.data
    ) {
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

  const requestClose = useCallback(
    (id?: string) => {
      if (!id) return;

      const tab =
        useEditorStore.getState().tabs[id];

      if (!tab) return;

      if (tab.content !== tab.savedContent) {
        setClosing(tab);
      } else {
        useEditorStore
          .getState()
          .closeTab(id);
      }
    },
    []
  );

  useKeyboardShortcuts({
    save: () => {
      if (!tabs.activeTabId) return;

      void saveNow(tabs.activeTabId).catch(
        (error) =>
          show(
            error instanceof Error
              ? error.message
              : "Save failed.",
            "error"
          )
      );
    },

    close: () =>
      requestClose(tabs.activeTabId),

    quickOpen: () =>
      setQuickOpen(true),
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
    ? explorer.entities.get(
        explorer.selectedId
      )
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
      await repositoryStatus.refetch();
      show(`${files.length} file${files.length === 1 ? "" : "s"} uploaded to ${selectedNode?.nodeType === "Folder" ? selectedNode.path : "/"}.`);
    } catch (error) {
      show(error instanceof Error ? error.message : "Upload failed.", "error");
    } finally {
      setUploading([]);
      if (fileInputRef.current) fileInputRef.current.value = "";
      if (imageInputRef.current) imageInputRef.current.value = "";
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
          newName.trim()
        );
      } else {
        await fileExplorerApi.createFolder(
          projectId,
          create.parentId,
          newName.trim()
        );
      }

      const createdType = create.type;

      setCreate(undefined);
      setNewName("");

      await reloadTree();

      show(
        `${
          createdType === "file"
            ? "File"
            : "Folder"
        } created.`
      );
    } catch (error) {
      show(
        error instanceof Error
          ? error.message
          : "Creation failed.",
        "error"
      );
    }
  };

  const actions = {
    onOpen: (node: WorkspaceNode) =>
      void tabs
        .openFile(node)
        .catch((error) =>
          show(
            error instanceof Error
              ? error.message
              : "Unable to open file.",
            "error"
          )
        ),

    onCreate: (
      type: "file" | "folder",
      parentId?: string
    ) => {
      setCreate({
        type,
        parentId,
      });

      setNewName("");
    },

    onRename: async (
      node: WorkspaceNode,
      name: string
    ) => {
      try {
        await fileExplorerApi.rename(
          node.id,
          name
        );

        await reloadTree();
      } catch (error) {
        show(
          error instanceof Error
            ? error.message
            : "Rename failed.",
          "error"
        );
      }
    },

    onDelete: setDeleting,

    onMove: async (
      nodeId: string,
      parentId?: string
    ) => {
      if (!nodeId) return;

      try {
        await fileExplorerApi.move(
          nodeId,
          parentId
        );

        await reloadTree();
      } catch (error) {
        show(
          error instanceof Error
            ? error.message
            : "Move failed.",
          "error"
        );
      }
    },
  };

  const resize = (
    side: "left" | "right",
    start: number
  ) => {
    const initialLeft = tabs.leftWidth;
    const initialRight = tabs.rightWidth;

    const move = (
      event: MouseEvent
    ) => {
      tabs.setPanelWidths(
        side === "left"
          ? Math.min(
              480,
              Math.max(
                180,
                initialLeft +
                  event.clientX -
                  start
              )
            )
          : initialLeft,

        side === "right"
          ? Math.min(
              480,
              Math.max(
                220,
                initialRight -
                  event.clientX +
                  start
              )
            )
          : initialRight
      );
    };

    const stop = () => {
      window.removeEventListener(
        "mousemove",
        move
      );

      window.removeEventListener(
        "mouseup",
        stop
      );
    };

    window.addEventListener(
      "mousemove",
      move
    );

    window.addEventListener(
      "mouseup",
      stop
    );
  };

  const files = [
    ...explorer.entities.values(),
  ].filter(
    (node) =>
      node.nodeType === "File" &&
      `${node.name} ${node.path}`
        .toLowerCase()
        .includes(
          quickFilter.toLowerCase()
        )
  );

  return (
    <main className="workspace-page">
      <header className="workspace-toolbar">
        <button
          onClick={() =>
            navigate(
              `/projects/${projectId}/settings`
            )
          }
        >
          ← Project
        </button>

        <strong>
          Collaborative Workspace
        </strong>

        <div>
          <button
            onClick={() =>
              setQuickOpen(true)
            }
          >
            ⌘P
          </button>

          <button
            onClick={() =>
              actions.onCreate(
                "file",
                createParentId
              )
            }
          >
            ＋ File
          </button>

          <button
            onClick={() =>
              actions.onCreate(
                "folder",
                createParentId
              )
            }
          >
            ＋ Folder
          </button>

          <button aria-label="Upload files" title="Upload files" onClick={() => fileInputRef.current?.click()}>
            ↑ File
          </button>

          <button aria-label="Upload image" title="Upload image" onClick={() => imageInputRef.current?.click()}>
            ▧ Image
          </button>

          <button
            className={
              rightMode === "ai" &&
              tabs.rightPanelVisible
                ? "active"
                : ""
            }
            onClick={() => {
              setRightMode("ai");

              if (
                !tabs.rightPanelVisible
              ) {
                tabs.toggleRightPanel();
              }
            }}
          >
            ✦ AI
          </button>

          <button
            onClick={() => {
              setRightMode(
                "collaboration"
              );

              if (
                !tabs.rightPanelVisible
              ) {
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
          gridTemplateColumns: `${
            tabs.leftWidth
          }px 4px minmax(280px, 1fr) ${
            tabs.rightPanelVisible
              ? `4px ${tabs.rightWidth}px`
              : ""
          }`,
        }}
      >
        <aside className={`explorer-panel ${dragActive ? "drop-active" : ""}`}
          onDragEnter={(event) => { if (event.dataTransfer.types.includes("Files")) { event.preventDefault(); setDragActive(true); } }}
          onDragOver={(event) => { if (event.dataTransfer.types.includes("Files")) event.preventDefault(); }}
          onDragLeave={(event) => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDragActive(false); }}
          onDrop={(event) => { if (!event.dataTransfer.files.length) return; event.preventDefault(); event.stopPropagation(); setDragActive(false); void uploadFiles(event.dataTransfer.files); }}>
          <header>
            <div className="workspace-left-tabs">
              <button className={leftMode === "explorer" ? "active" : ""} onClick={() => setLeftMode("explorer")}>EXPLORER</button>
              <button className={leftMode === "source" ? "active" : ""} onClick={() => setLeftMode("source")}>SOURCE {repositoryStatus.data?.files.length ? `(${repositoryStatus.data.files.length})` : ""}</button>
            </div>
            <div className="explorer-actions">
              <button aria-label="New file" title="New file" onClick={() => actions.onCreate("file", createParentId)}>＋</button>
              <button aria-label="New folder" title="New folder" onClick={() => actions.onCreate("folder", createParentId)}>▱</button>
              <button aria-label="Upload files" title="Upload files" onClick={() => fileInputRef.current?.click()}>↑</button>
              <button aria-label="Upload image" title="Upload image" onClick={() => imageInputRef.current?.click()}>▧</button>
              <button aria-label="Refresh explorer" title="Refresh explorer" onClick={() => reloadTree()}>↻</button>
            </div>
          </header>

          <input ref={fileInputRef} className="workspace-file-input" type="file" multiple onChange={(event) => event.target.files && void uploadFiles(event.target.files)} />
          <input ref={imageInputRef} className="workspace-file-input" type="file" multiple accept="image/png,image/jpeg,image/webp,image/gif" onChange={(event) => event.target.files && void uploadFiles(event.target.files)} />
          {leftMode === "explorer" && <div className="upload-destination">Upload to: {selectedNode?.nodeType === "Folder" ? selectedNode.path : "/"}</div>}
          {uploading.length > 0 && <div className="upload-progress" role="status"><strong>Uploading…</strong>{uploading.map((name) => <span key={name}>{name}</span>)}</div>}
          {dragActive && <div className="file-drop-overlay">Drop files to upload</div>}

          {leftMode === "source" ? (
            <div className="source-control-panel">
              <strong>⎇ {repositoryStatus.data?.currentBranch || "main"}</strong>
              <textarea aria-label="Commit message" placeholder="Commit message" value={commitMessage} onChange={(event) => setCommitMessage(event.target.value)} />
              <button disabled={!commitMessage.trim() || repositoryStatus.data?.isClean} onClick={async () => { try { await repositoryApi.commit(projectId, commitMessage.trim()); setCommitMessage(""); await repositoryStatus.refetch(); show("Commit created."); } catch (error) { show(error instanceof Error ? error.message : "Commit failed.", "error"); } }}>Commit</button>
              <div className="source-changes">{repositoryStatus.isLoading ? <span>Loading Git status…</span> : repositoryStatus.data?.files.length ? repositoryStatus.data.files.map((file) => <div key={file.path}><code>{file.indexStatus}{file.workingTreeStatus}</code><span>{file.path}</span></div>) : <span>No changes</span>}</div>
            </div>
          ) : tree.isLoading ? (
            <div className="tree-empty">
              Loading files…
            </div>
          ) : (
            <FileTree {...actions} />
          )}
        </aside>

        <button
          className="panel-resizer"
          aria-label="Resize explorer"
          onMouseDown={(event) =>
            resize(
              "left",
              event.clientX
            )
          }
        />

        <section className="editor-shell">
          <div
            className="editor-tabs"
            role="tablist"
          >
            {tabs.openTabIds.map(
              (id) => {
                const tab =
                  tabs.tabs[id];

                const dirty =
                  tab.content !==
                  tab.savedContent;

                return (
                  <button
                    key={id}
                    role="tab"
                    aria-selected={
                      id ===
                      tabs.activeTabId
                    }
                    className={
                      id ===
                      tabs.activeTabId
                        ? "active"
                        : ""
                    }
                    onClick={() =>
                      tabs.activateTab(
                        id
                      )
                    }
                  >
                    <span>
                      {tab.name}
                    </span>

                    {dirty && (
                      <i title="Unsaved changes">
                        ●
                      </i>
                    )}

                    <b
                      onClick={(
                        event
                      ) => {
                        event.stopPropagation();

                        requestClose(
                          id
                        );
                      }}
                    >
                      ×
                    </b>
                  </button>
                );
              }
            )}
          </div>

          <div className="monaco-host">
            {tabs.activeTab?.viewer === "image" ? (
              <div className="image-viewer">
                <div className="image-viewer-toolbar">
                  <strong>{tabs.activeTab.name}</strong>
                  <button onClick={() => void navigator.clipboard.writeText(tabs.activeTab!.path)}>Copy path</button>
                  <a href={tabs.activeTab.objectUrl} download={tabs.activeTab.name}>Download</a>
                </div>
                <img src={tabs.activeTab.objectUrl} alt={tabs.activeTab.name} />
                <small>{tabs.activeTab.path}</small>
              </div>
            ) : tabs.activeTab ? (
              <MonacoPane
                projectId={projectId}
                tab={tabs.activeTab}
                onSelectionChange={
                  handleSelectionChange
                }
              />
            ) : (
              <div className="editor-empty">
                <span className="brand-mark">
                  C
                </span>

                <h2>
                  Select a file to
                  begin
                </h2>

                <p>
                  Open a file, upload source code, or add an image.
                </p>
                <div className="editor-empty-actions">
                  <button onClick={() => actions.onCreate("file", createParentId)}>New File</button>
                  <button onClick={() => fileInputRef.current?.click()}>Upload File</button>
                  <button onClick={() => imageInputRef.current?.click()}>Upload Image</button>
                </div>
              </div>
            )}
          </div>

          <footer className="editor-statusbar">
            <span>
              {tabs.activeTab
                ?.status ?? "Ready"}
            </span>

            <span>
              {tabs.activeTab
                ? `Ln ${tabs.activeTab.cursor.lineNumber}, Col ${tabs.activeTab.cursor.column}`
                : ""}
            </span>

            <span>
              {tabs.activeTab
                ?.language ??
                "Plain Text"}
            </span>

            <label>
              Font

              <input
                type="number"
                min="10"
                max="28"
                value={
                  tabs.fontSize
                }
                onChange={(
                  event
                ) =>
                  tabs.setFontSize(
                    Number(
                      event.target
                        .value
                    )
                  )
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
              onMouseDown={(event) =>
                resize(
                  "right",
                  event.clientX
                )
              }
            />

            <aside className="collaboration-panel">
              {rightMode ===
              "ai" ? (
                <AiAssistantPanel
                  projectId={
                    projectId
                  }
                  fileId={
                    tabs.activeTab
                      ?.id
                  }
                  fileName={
                    tabs.activeTab
                      ?.name
                  }
                  language={
                    tabs.activeTab
                      ?.language
                  }
                  selectedCode={
                    selectedCode
                  }
                  fileContent={
                    tabs.activeTab
                      ?.content
                  }
                  onApplySuggestion={
                    setAiSuggestion
                  }
                />
              ) : (
                <>
                  <PresencePanel />

                  {tabs.activeTab ? (
                    <VersionHistory
                      nodeId={
                        tabs.activeTab
                          .id
                      }
                      refreshKey={
                        historyKey
                      }
                      onRestore={async () => {
                        const fresh =
                          await fileExplorerApi.content(
                            tabs
                              .activeTab!
                              .id
                          );

                        crdtDocumentManager.reset(
                          fresh.nodeId,
                          fresh.content
                        );

                        tabs.acceptExternal(
                          fresh.nodeId,
                          fresh.content,
                          fresh.concurrencyToken
                        );

                        setHistoryKey(
                          (key) =>
                            key + 1
                        );

                        show(
                          "Version restored."
                        );
                      }}
                    />
                  ) : (
                    <div className="tree-empty">
                      Open a file to
                      view
                      collaboration
                      and history.
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
        title={`New ${
          create?.type ?? "node"
        }`}
        onClose={() =>
          setCreate(undefined)
        }
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() =>
                setCreate(
                  undefined
                )
              }
            >
              Cancel
            </button>

            <button
              className="ui-button primary"
              onClick={
                createNode
              }
            >
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
              onChange={(
                event
              ) =>
                setNewName(
                  event.target
                    .value
                )
              }
              onKeyDown={(
                event
              ) => {
                if (
                  event.key ===
                  "Enter"
                ) {
                  void createNode();
                }
              }}
            />
          </label>
        </div>
      </Dialog>

      <ConfirmDialog
        open={Boolean(deleting)}
        title={`Delete ${
          deleting?.name ??
          "node"
        }?`}
        description={
          deleting?.nodeType ===
          "Folder"
            ? "This folder and every descendant will be soft-deleted."
            : "The file and history will be hidden."
        }
        destructive
        confirmLabel="Delete"
        onClose={() =>
          setDeleting(undefined)
        }
        onConfirm={async () => {
          if (!deleting) return;

          try {
            await fileExplorerApi.remove(
              deleting.id
            );

            if (
              tabs.tabs[
                deleting.id
              ]
            ) {
              tabs.closeTab(
                deleting.id
              );
            }

            setDeleting(
              undefined
            );

            await reloadTree();
          } catch (error) {
            show(
              error instanceof Error
                ? error.message
                : "Delete failed.",
              "error"
            );
          }
        }}
      />

      <ConfirmDialog
        open={Boolean(
          aiSuggestion
        )}
        title="Apply AI suggestion?"
        description="This replaces the current editor content. Review the suggestion first; the file will not be saved automatically."
        confirmLabel="Apply to editor"
        onClose={() =>
          setAiSuggestion(
            undefined
          )
        }
        onConfirm={() => {
          if (
            aiSuggestion &&
            tabs.activeTabId
          ) {
            crdtDocumentManager.reset(
              tabs.activeTabId,
              aiSuggestion
            );
          }

          setAiSuggestion(
            undefined
          );
        }}
      />

      <Dialog
        open={Boolean(closing)}
        title={`Save changes to ${
          closing?.name ?? ""
        }?`}
        description="Your changes will be lost if you close without saving."
        onClose={() =>
          setClosing(undefined)
        }
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() =>
                setClosing(
                  undefined
                )
              }
            >
              Cancel
            </button>

            <button
              className="ui-button danger"
              onClick={() => {
                if (closing) {
                  tabs.discardChanges(
                    closing.id
                  );

                  tabs.closeTab(
                    closing.id
                  );
                }

                setClosing(
                  undefined
                );
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
                  await saveNow(
                    closing.id
                  );

                  tabs.closeTab(
                    closing.id
                  );

                  setClosing(
                    undefined
                  );
                } catch (error) {
                  show(
                    error instanceof
                      Error
                      ? error.message
                      : "Save failed.",
                    "error"
                  );
                }
              }}
            >
              Save
            </button>
          </>
        }
      >
        <div className="confirmation-note">
          The server concurrency
          token will be checked before
          saving.
        </div>
      </Dialog>

      <Dialog
        open={quickOpen}
        title="Quick Open"
        onClose={() =>
          setQuickOpen(false)
        }
      >
        <div className="quick-open">
          <input
            autoFocus
            value={quickFilter}
            onChange={(event) =>
              setQuickFilter(
                event.target.value
              )
            }
            placeholder="Search files by name or path…"
          />

          {files
            .slice(0, 20)
            .map((node) => (
              <button
                key={node.id}
                onClick={() => {
                  void tabs.openFile(
                    node
                  );

                  setQuickOpen(
                    false
                  );

                  setQuickFilter(
                    ""
                  );
                }}
              >
                <strong>
                  {node.name}
                </strong>

                <small>
                  {node.path}
                </small>
              </button>
            ))}
        </div>
      </Dialog>
    </main>
  );
}
