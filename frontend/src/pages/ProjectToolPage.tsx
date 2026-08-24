import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { DatabaseExplorer } from "../features/database-explorer/DatabaseExplorer";
import { ApprovalCenter } from "../features/ai-approvals/ApprovalCenter";
import { useQuery } from "@tanstack/react-query";
import { repositoryApi, type GitCommit } from "../features/repository/api";
import { databaseMetadataApi } from "../features/database-explorer/api";
import { useProject } from "../features/projects/hooks";
import { queryKeys } from "../services/queryKeys";
import { Dialog } from "../components/ui/Dialog";

export type ProjectTool = "architecture" | "database" | "api" | "versions" | "approvals" | "billing";

type OpenApiDocument = {
  info?: { title?: string; version?: string };
  paths?: Record<string, Record<string, { summary?: string; description?: string; tags?: string[]; security?: unknown[] }>>;
};

const toolCopy: Record<ProjectTool, { eyebrow: string; title: string; description: string }> = {
  architecture: { eyebrow: "SYSTEM MAP", title: "Architecture", description: "A live-oriented view of the services that make up this NexaCode workspace." },
  database: { eyebrow: "DATA MODEL", title: "Workspace database", description: "Choose a database engine and inspect this project’s isolated schema blueprint, relationships, constraints, and indexes." },
  api: { eyebrow: "OPENAPI", title: "API reference", description: "Generated from the backend OpenAPI document, so the reference stays aligned with running code." },
  versions: { eyebrow: "SOURCE CONTROL", title: "Version history", description: "File versions are available inside the workspace. Repository-wide Git integration has not been configured for this project." },
  approvals: { eyebrow: "AI GOVERNANCE", title: "AI approvals", description: "High-risk AI operations require an explicit decision before any tool can change project state." },
  billing: { eyebrow: "SUBSCRIPTION", title: "Billing & usage", description: "Plan and usage visibility without simulated payments or unsupported financial actions." },
};

function EmptyCapability({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="nexa-empty"><span>◇</span><h2>{title}</h2><p>{children}</p></section>;
}

function ArchitectureView({ projectId }: { projectId: string }) {
  const project = useProject(projectId);
  const database = useQuery({ queryKey: queryKeys.database(projectId), queryFn: () => databaseMetadataApi.schema(projectId) });
  const nodes = [
    [project.data?.name ?? "Workspace", project.data?.defaultLanguage ?? "Project", "Application"],
    ["Workspace files", "Editor · version history", "Source"],
    [database.data?.provider ?? "Database not configured", database.data?.isConfigured ? `${database.data.schemas.length} schema` : "Choose in Database", "Data"],
    ["Git repository", "Branches · commits · diffs", "Source control"],
    ["AI assistant", "Project context · approvals", "Intelligence"],
  ];
  return <div className="architecture-canvas" aria-label="NexaCode system architecture">
    <div className="architecture-flow" aria-hidden="true" />
    {nodes.map(([name, technology, type], index) => <article className={`architecture-node node-${index + 1}`} key={`${type}-${name}`}>
      <div><span className="status-dot" />{type}</div><h2>{name}</h2><code>{technology}</code>
    </article>)}
    <div className="architecture-legend"><span><i className="status-dot" /> configured</span><span>Connections show primary data flow</span></div>
  </div>;
}

function VersionHistoryView({ projectId }: { projectId: string }) {
  const [selected, setSelected] = useState<GitCommit>();
  const history = useQuery({ queryKey: queryKeys.repository.history(projectId), queryFn: () => repositoryApi.history(projectId, 100) });
  const diff = useQuery({ queryKey: queryKeys.repository.commitDiff(projectId, selected?.sha ?? ""), queryFn: () => repositoryApi.commitDiff(projectId, selected!.sha), enabled: Boolean(selected) });
  if (history.isLoading) return <div className="nexa-loading" role="status">Loading project history…</div>;
  if (history.isError) return <section className="nexa-empty"><span>!</span><h2>Version history unavailable</h2><p>{history.error instanceof Error ? history.error.message : "History could not be loaded."}</p><button className="nexa-secondary-action" onClick={() => void history.refetch()}>Retry</button></section>;
  if (!history.data?.length) return <EmptyCapability title="No commits yet">Save project files and create the first commit from Source Control in the workspace.</EmptyCapability>;
  return <><div className="project-version-history"><header><strong>{history.data.length} commits</strong><span>History for this workspace repository</span></header>{history.data.map(commit => <button key={commit.sha} onClick={() => setSelected(commit)}><code>{commit.shortSha}</code><div><strong>{commit.message}</strong><span>{commit.authorName} · {new Date(commit.committedAt).toLocaleString()}</span></div><i>View changes →</i></button>)}</div><Dialog open={Boolean(selected)} title={selected ? `${selected.shortSha} · ${selected.message}` : "Commit changes"} onClose={() => setSelected(undefined)}><div className="commit-detail">{diff.isLoading ? <div className="nexa-loading">Loading changes…</div> : diff.isError ? <p>Changes could not be loaded.</p> : <pre>{diff.data?.patch || "This commit has no textual diff."}</pre>}</div></Dialog></>;
}

function ApiView() {
  const [document, setDocument] = useState<OpenApiDocument | null>(null);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    fetch("/swagger/v1/swagger.json", { signal: controller.signal })
      .then(async response => { if (!response.ok) throw new Error(`OpenAPI returned ${response.status}`); return response.json() as Promise<OpenApiDocument>; })
      .then(setDocument).catch(reason => { if (reason instanceof Error && reason.name !== "AbortError") setError(reason.message); });
    return () => controller.abort();
  }, []);
  const endpoints = useMemo(() => Object.entries(document?.paths ?? {}).flatMap(([path, methods]) => Object.entries(methods).filter(([method]) => ["get", "post", "put", "patch", "delete"].includes(method)).map(([method, operation]) => ({ path, method, ...operation }))).filter(item => `${item.method} ${item.path} ${item.summary ?? ""}`.toLowerCase().includes(query.toLowerCase())), [document, query]);
  if (error) return <EmptyCapability title="OpenAPI is unavailable">Start the backend API and refresh this page. No endpoint data has been invented.</EmptyCapability>;
  if (!document) return <div className="nexa-loading" role="status">Loading OpenAPI metadata…</div>;
  return <div className="api-reference">
    <header><div><strong>{document.info?.title ?? "Coding API"}</strong><span>Version {document.info?.version ?? "current"}</span></div><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Filter endpoints…" aria-label="Filter API endpoints" /></header>
    <div className="api-endpoints">{endpoints.map(endpoint => <article key={`${endpoint.method}-${endpoint.path}`}><span className={`method method-${endpoint.method}`}>{endpoint.method}</span><code>{endpoint.path}</code><div><strong>{endpoint.summary ?? "Documented endpoint"}</strong><p>{endpoint.description ?? (endpoint.security ? "Authentication required" : "See OpenAPI schema for request and response contracts.")}</p></div></article>)}</div>
  </div>;
}

function ToolContent({ tool, projectId }: { tool: ProjectTool; projectId: string }) {
  if (tool === "architecture") return <ArchitectureView projectId={projectId} />;
  if (tool === "api") return <ApiView />;
  if (tool === "database") return <DatabaseExplorer projectId={projectId} />;
  if (tool === "versions") return <VersionHistoryView projectId={projectId} />;
  if (tool === "approvals") return <div className="approval-workflow"><div className="workflow-steps">{["Request", "Context", "Analysis", "Proposal", "Review", "Apply", "Validate"].map((step, index) => <div key={step}><span>{index + 1}</span><strong>{step}</strong></div>)}</div><ApprovalCenter projectId={projectId}/></div>;
  return <div className="billing-grid"><article><span>CURRENT PLAN</span><h2>Workspace plan</h2><p>Subscription data is not connected to a payment provider.</p><button disabled title="No payment provider is configured">Manage plan</button></article><article><span>AI USAGE</span><h2>Usage metering ready</h2><p>AI usage records are stored by the backend. Customer billing is intentionally unavailable.</p></article></div>;
}

export function ProjectToolPage({ tool }: { tool: ProjectTool }) {
  const { projectId = "" } = useParams();
  const copy = toolCopy[tool];
  return <main className="nexa-tool-page"><header className="nexa-page-header"><div><p>{copy.eyebrow}</p><h1>{copy.title}</h1><span>{copy.description}</span></div><Link to={`/projects/${projectId}/workspace`} className="nexa-secondary-action">Return to workspace</Link></header><ToolContent tool={tool} projectId={projectId} /></main>;
}
