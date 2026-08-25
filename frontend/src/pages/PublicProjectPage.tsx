import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ErrorState, LoadingState } from "../components/AsyncState";
import {
  usersApi,
  userKeys,
  type PublicProjectNode,
} from "../features/users/api";
import { moderationApi } from "../features/moderation/api";
import { useToast } from "../contexts/ToastContext";
import { savedApi } from "../features/saved/api";
import { queryKeys } from "../services/queryKeys";

function orderedTree(nodes: PublicProjectNode[]) {
  const result: Array<PublicProjectNode & { depth: number }> = [];
  const visit = (parentId: string | undefined, depth: number) =>
    nodes
      .filter((node) => node.parentId === parentId)
      .sort(
        (a, b) =>
          Number(a.nodeType === "File") - Number(b.nodeType === "File") ||
          a.name.localeCompare(b.name),
      )
      .forEach((node) => {
        result.push({ ...node, depth });
        if (node.nodeType === "Folder") visit(node.id, depth + 1);
      });
  visit(undefined, 0);
  return result;
}

export function PublicProjectPage() {
  const { projectId = "" } = useParams();
  const { show } = useToast();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const details = useQuery({
    queryKey: userKeys.publicProject(projectId),
    queryFn: () => usersApi.publicProject(projectId),
  });
  const tree = useQuery({
    queryKey: [...userKeys.publicProject(projectId), "tree"],
    queryFn: () => usersApi.publicProjectTree(projectId),
  });
  const [selectedId, setSelectedId] = useState<string>();
  const savedKey = queryKeys.saved.project(projectId);
  const saved = useQuery({
    queryKey: savedKey,
    queryFn: () =>
      savedApi
        .list("Projects")
        .then((x) => x.projects.some((p) => p.id === projectId)),
  });
  const saveProject = useMutation({
    mutationFn: () => savedApi.project(projectId, !saved.data),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: savedKey });
      const previous = saved.data;
      queryClient.setQueryData(savedKey, !previous);
      return { previous };
    },
    onError: (error, _variables, context) => {
      queryClient.setQueryData(savedKey, context?.previous);
      show(error.message, "error");
    },
    onSuccess: (value) => {
      queryClient.setQueryData(savedKey, value);
      void queryClient.invalidateQueries({ queryKey: queryKeys.saved.all });
      show(value ? "Project saved." : "Project removed from saved.");
    },
  });
  const forkProject = useMutation({
    mutationFn: () => usersApi.forkProject(projectId),
    onSuccess: (value) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.projects });
      show("Private fork created.");
      navigate(`/projects/${value.projectId}/workspace`);
    },
    onError: (error) => show(error.message, "error"),
  });
  const nodes = useMemo(() => orderedTree(tree.data ?? []), [tree.data]);
  const selectedFileId = nodes.some(
    (node) => node.id === selectedId && node.nodeType === "File",
  )
    ? selectedId
    : undefined;
  useEffect(() => {
    if (tree.isPending) return;
    const preferredFile =
      nodes.find(
        (node) => node.nodeType === "File" && /^readme(\.|$)/i.test(node.name),
      ) ?? nodes.find((node) => node.nodeType === "File");
    if (!selectedFileId && preferredFile) setSelectedId(preferredFile.id);
    if (!preferredFile && selectedId) setSelectedId(undefined);
  }, [nodes, selectedFileId, selectedId, tree.isPending]);
  const file = useQuery({
    queryKey: [...userKeys.publicProject(projectId), "file", selectedFileId],
    queryFn: () => usersApi.publicProjectFile(projectId, selectedFileId!),
    enabled: Boolean(selectedFileId),
  });
  if (details.isPending || tree.isPending)
    return (
      <main className="public-repository-page">
        <LoadingState label="Loading repository…" />
      </main>
    );
  if (details.isError)
    return (
      <main className="public-repository-page">
        <ErrorState
          message={details.error.message}
          retry={() => details.refetch()}
        />
      </main>
    );
  if (tree.isError)
    return (
      <main className="public-repository-page">
        <ErrorState message={tree.error.message} retry={() => tree.refetch()} />
      </main>
    );
  const project = details.data;
  return (
    <main className="public-repository-page">
      <header className="public-repository-header">
        <p className="public-repository-eyebrow">Public repository</p>
        <div className="public-repository-title">
          <span className="public-repository-mark" aria-hidden="true">
            {"</>"}
          </span>
          <div>
            <div className="public-repository-path">
              {project.ownerPublicId ? (
                <Link
                  to={`/users/${encodeURIComponent(project.ownerPublicId)}`}
                >
                  {project.ownerDisplayName}
                </Link>
              ) : (
                <strong>{project.ownerDisplayName}</strong>
              )}
              <span>/</span>
              <h1>{project.name}</h1>
              <b>Public</b>
            </div>
            <p>{project.description || "No description provided."}</p>
          </div>
        </div>
        <div className="public-repository-meta">
          <span>{project.defaultLanguage || "Other"}</span>
          <span>
            Updated {new Date(project.updatedAt).toLocaleDateString()}
          </span>
          <span>
            {nodes.filter((node) => node.nodeType === "File").length} files
          </span>
          <button
            disabled={saveProject.isPending}
            onClick={() => saveProject.mutate()}
          >
            {saved.data ? "Saved" : "Save"}
          </button>
          <button
            disabled={forkProject.isPending}
            onClick={() => {
              if (confirm("Create a private editable fork of this repository?"))
                forkProject.mutate();
            }}
          >
            {forkProject.isPending ? "Forking…" : "Fork"}
          </button>
          <button
            onClick={() => {
              const reason = window
                .prompt(
                  "Report reason: Spam, Harassment, Hate or abuse, Dangerous content, Privacy, Copyright, Impersonation, Other",
                  "Spam",
                )
                ?.trim();
              if (reason)
                void moderationApi
                  .report("Project", project.id, reason)
                  .then(() => show("Project report submitted."))
                  .catch((error) => show(error.message, "error"));
            }}
          >
            Report
          </button>
        </div>
      </header>

      <div className="public-repository-layout">
        <aside className="public-repository-tree">
          <header>
            <div>
              <span aria-hidden="true">⌘</span>
              <h2>Files</h2>
            </div>
            <small>
              {nodes.filter((node) => node.nodeType === "File").length}
            </small>
          </header>
          {nodes.length ? (
            <nav aria-label="Repository files">
              {nodes.map((node) => (
                <button
                  key={node.id}
                  type="button"
                  disabled={node.nodeType === "Folder"}
                  className={selectedId === node.id ? "active" : ""}
                  style={{ paddingLeft: `${1 + node.depth * 1.1}rem` }}
                  onClick={() =>
                    node.nodeType === "File" && setSelectedId(node.id)
                  }
                >
                  <span aria-hidden="true">
                    {node.nodeType === "Folder" ? "▸" : "⌑"}
                  </span>
                  <span>{node.name}</span>
                </button>
              ))}
            </nav>
          ) : (
            <div className="public-tree-empty">
              <span aria-hidden="true">{"</>"}</span>
              <strong>Empty repository</strong>
              <p>No public files have been added yet.</p>
            </div>
          )}
        </aside>

        <section className="public-file-viewer">
          <header>
            <div>
              <span aria-hidden="true">{file.data ? "⌑" : "◇"}</span>
              <strong>{file.data?.path ?? "File preview"}</strong>
            </div>
            {file.data && <small>Version {file.data.versionNumber}</small>}
          </header>
          {!selectedFileId ? (
            <div className="public-file-empty">
              <span aria-hidden="true">{"</>"}</span>
              <h3>{nodes.length ? "Select a file" : "Repository is empty"}</h3>
              <p>
                {nodes.length
                  ? "Choose a file from the explorer to preview its contents."
                  : "There are no files available to preview."}
              </p>
            </div>
          ) : file.isPending ? (
            <LoadingState label="Loading file…" />
          ) : file.isError ? (
            <ErrorState
              message={file.error.message}
              retry={() => file.refetch()}
            />
          ) : file.data ? (
            <pre>
              <code>{file.data.content}</code>
            </pre>
          ) : (
            <div className="public-file-empty">
              Select a file to preview its contents.
            </div>
          )}
        </section>
      </div>
    </main>
  );
}
