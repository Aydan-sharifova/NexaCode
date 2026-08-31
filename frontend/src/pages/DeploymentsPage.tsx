import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";
import { deploymentKeys, deploymentsApi } from "../features/deployments/api";
import { useProject } from "../features/projects/hooks";
import { shareUrl } from "../utils/shareUrl";

export function DeploymentsPage() {
  const { projectId = "" } = useParams(),
    client = useQueryClient(),
    { show } = useToast(),
    project = useProject(projectId);
  const shareDeployment = async (url: string) => {
    const absoluteUrl = new URL(url, window.location.origin).href;
    try {
      const result = await shareUrl(
        project.data?.name ?? "Deployment",
        absoluteUrl,
      );
      if (result === "copied") show("Deployment link copied.");
    } catch {
      show("Deployment link could not be shared.", "error");
    }
  };
  const canDeploy = Boolean(
    project.data?.isPublic &&
    !project.data.isReadOnly &&
    project.data.currentUserRole !== "Viewer",
  );
  const list = useQuery({
    queryKey: deploymentKeys.list(projectId),
    queryFn: () => deploymentsApi.list(projectId),
  });
  const deploy = useMutation({
    mutationFn: () => deploymentsApi.deploy(projectId),
    onSuccess: (value) => {
      void client.invalidateQueries({
        queryKey: deploymentKeys.list(projectId),
      });
      show(`Deployment v${value.version} published.`);
    },
    onError: (error) => show(error.message, "error"),
  });
  return (
    <main className="dashboard-content feature-page">
      <header className="feature-heading">
        <div>
          <Link className="back-link" to={`/projects/${projectId}/workspace`}>
            ← Workspace
          </Link>
          <h1>Deployments</h1>
          <p>
            Immutable static snapshots from the current saved workspace. Public
            projects only.
          </p>
          {project.data && !canDeploy ? (
            <small role="status">
              Deployment requires a writable public project and repository-write
              role.
            </small>
          ) : null}
        </div>
        <button
          className="create-button"
          disabled={deploy.isPending || project.isLoading || !canDeploy}
          onClick={() => {
            if (
              confirm("Publish the current saved index.html and static assets?")
            )
              deploy.mutate();
          }}
        >
          {deploy.isPending ? "Deploying…" : "Deploy current version"}
        </button>
      </header>
      {list.isLoading ? (
        <LoadingState label="Loading deployments…" />
      ) : list.isError ? (
        <ErrorState
          message={list.error.message}
          retry={() => void list.refetch()}
        />
      ) : !list.data?.length ? (
        <EmptyState
          title="No deployments yet"
          description="A root index.html is required. Deployments are versioned and source-hashed."
        />
      ) : (
        <section className="room-grid">
          {list.data.map((item) => (
            <article className="room-card" key={item.id}>
              <span
                className={`room-status ${item.isActive ? "active" : "completed"}`}
              >
                {item.isActive ? "Live" : "Superseded"}
              </span>
              <small>Version {item.version}</small>
              <h2>{item.slug}</h2>
              <p>
                Source <code>{item.sourceHash.slice(0, 12)}</code>
                {item.commitSha ? (
                  <>
                    {" "}
                    · Commit <code>{item.commitSha.slice(0, 8)}</code>
                  </>
                ) : null}
              </p>
              <footer>
                <span>{new Date(item.deployedAt).toLocaleString()}</span>
                <span className="deployment-actions">
                  <button
                    type="button"
                    onClick={() => void shareDeployment(item.url)}
                  >
                    Share
                  </button>
                  <a href={item.url} target="_blank" rel="noreferrer">
                    Open deployment ↗
                  </a>
                </span>
              </footer>
            </article>
          ))}
        </section>
      )}
    </main>
  );
}
