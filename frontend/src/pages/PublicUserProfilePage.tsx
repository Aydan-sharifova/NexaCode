import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import { usersApi, userKeys } from "../features/users/api";
import { chatApi } from "../features/chat/api";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";

export function PublicUserProfilePage() {
  const { publicId = "" } = useParams();
  const identifier = decodeURIComponent(publicId).trim().replace(/^@/, "");
  const isValidIdentifier = Boolean(identifier) && identifier !== "undefined" && identifier !== "null";
  const navigate = useNavigate();
  const { show } = useToast();
  const client = useQueryClient();
  const profile = useQuery({ queryKey: userKeys.profile(identifier), queryFn: () => usersApi.profile(identifier), enabled: isValidIdentifier });
  const projects = useQuery({ queryKey: userKeys.publicProjects(identifier), queryFn: () => usersApi.publicProjects(identifier), enabled: isValidIdentifier });
  const message = useMutation({ mutationFn: () => chatApi.direct(profile.data?.publicId ?? identifier), onSuccess: async conversation => { await client.invalidateQueries({ queryKey: ["chat-conversations"] }); navigate(`/chat?conversation=${conversation.id}`); }, onError: error => show(error.message, "error") });
  if (!isValidIdentifier) return <main className="public-profile-page"><ErrorState message="This profile link is invalid. Select the user again from Chat or Team." retry={() => navigate("/team")} /></main>;
  if (profile.isPending) return <main className="public-profile-page"><LoadingState label="Loading profile…" /></main>;
  if (profile.isError) return <main className="public-profile-page"><ErrorState message={profile.error.message} retry={() => profile.refetch()} /></main>;
  const user = profile.data;
  const copy = async () => { try { await navigator.clipboard.writeText(user.publicId); show("Public ID copied."); } catch { show("Unable to copy the public ID.", "error"); } };
  return (
    <main className="public-profile-page">
      <section className="public-profile-hero">
        <div className="public-profile-avatar" aria-hidden="true">
          {user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : user.displayName.slice(0, 1).toUpperCase()}
        </div>

        <div className="public-profile-details">
          <p className="public-profile-eyebrow">Public profile</p>
          <div className="public-profile-title">
            <h1>{user.displayName}</h1>
            <span>@{user.userName}</span>
          </div>
          <button className="public-id-badge" type="button" onClick={() => void copy()} title="Copy public ID">
            <span>@{user.publicId}</span>
            <small>Copy ID</small>
          </button>
          <p className="public-profile-bio">{user.bio || "No biography added yet."}</p>
          <div className="public-profile-meta">
            <span><strong>{user.publicProjectCount}</strong> public {user.publicProjectCount === 1 ? "project" : "projects"}</span>
            <span>Joined {new Date(user.joinedAt).toLocaleDateString()}</span>
          </div>
        </div>

        <div className="public-profile-actions">
          <button className="ui-button primary" disabled={message.isPending} onClick={() => message.mutate()}>
            {message.isPending ? "Opening…" : "Message user"}
          </button>
        </div>
      </section>

      <section className="public-projects">
        <header className="public-projects-header">
          <div>
            <p>Repositories</p>
            <h2>Public projects</h2>
          </div>
          <span>{user.publicProjectCount}</span>
        </header>

        {projects.isPending ? (
          <LoadingState label="Loading public projects…" />
        ) : projects.isError ? (
          <ErrorState message={projects.error.message} retry={() => projects.refetch()} />
        ) : projects.data?.items.length ? (
          <div className="public-project-grid">
            {projects.data.items.map(project => (
              <Link to={`/public/projects/${project.id}`} className="public-project-card" key={project.id}>
                <div className="public-project-card-head">
                  <span className="public-project-icon" aria-hidden="true">{"</>"}</span>
                  <span className="public-language-badge">{project.defaultLanguage || "Other"}</span>
                </div>
                <h3>{project.name}</h3>
                <p>{project.description || "No description provided."}</p>
                <small>Updated {new Date(project.updatedAt).toLocaleDateString()}</small>
              </Link>
            ))}
          </div>
        ) : (
          <div className="public-project-empty">
            <span aria-hidden="true">{"</>"}</span>
            <h3>No public projects yet</h3>
            <p>This user's public projects will appear here.</p>
          </div>
        )}
      </section>
    </main>
  );
}
