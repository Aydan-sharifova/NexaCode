import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router-dom";
import { usersApi, userKeys } from "../features/users/api";
import { chatApi } from "../features/chat/api";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";

export function PublicUserProfilePage() {
  const { publicId = "" } = useParams(); const navigate = useNavigate(); const { show } = useToast(); const client = useQueryClient();
  const profile = useQuery({ queryKey: userKeys.profile(publicId), queryFn: () => usersApi.profile(publicId) });
  const projects = useQuery({ queryKey: userKeys.publicProjects(publicId), queryFn: () => usersApi.publicProjects(publicId) });
  const message = useMutation({ mutationFn: () => chatApi.direct(publicId), onSuccess: async conversation => { await client.invalidateQueries({ queryKey: ["chat-conversations"] }); navigate(`/chat?conversation=${conversation.id}`); }, onError: error => show(error.message, "error") });
  if (profile.isPending) return <main className="public-profile-page"><LoadingState label="Loading profile…" /></main>;
  if (profile.isError) return <main className="public-profile-page"><ErrorState message={profile.error.message} retry={() => profile.refetch()} /></main>;
  const user = profile.data;
  const copy = async () => { try { await navigator.clipboard.writeText(user.publicId); show("Public ID copied."); } catch { show("Unable to copy the public ID.", "error"); } };
  return <main className="public-profile-page">
    <section className="public-profile-hero">
      <div className="public-profile-avatar">{user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : user.displayName.slice(0, 1)}</div>
      <div><p>PUBLIC PROFILE</p><h1>{user.displayName}</h1><button className="public-id-badge" onClick={() => void copy()}>@{user.publicId} · Copy</button><span>@{user.userName}</span><p>{user.bio || "No biography added yet."}</p><small>{user.publicProjectCount} public projects · Joined {new Date(user.joinedAt).toLocaleDateString()}</small></div>
      <button className="ui-button primary" disabled={message.isPending} onClick={() => message.mutate()}>{message.isPending ? "Opening…" : "Message"}</button>
    </section>
    <section className="public-projects"><h2>Public projects</h2>{projects.isPending ? <LoadingState label="Loading public projects…" /> : projects.data?.items.length ? <div>{projects.data.items.map(project => <article key={project.id}><span>{project.defaultLanguage}</span><h3>{project.name}</h3><p>{project.description || "No description."}</p><small>Updated {new Date(project.updatedAt).toLocaleDateString()}</small></article>)}</div> : <p>No public projects yet.</p>}</section>
  </main>;
}
