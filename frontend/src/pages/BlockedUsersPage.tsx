import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";
import { usersApi } from "../features/users/api";

export function BlockedUsersPage() {
  const client = useQueryClient(); const { show } = useToast();
  const blocked = useQuery({ queryKey: ["users", "blocked"], queryFn: usersApi.blocked });
  const unblock = useMutation({ mutationFn: usersApi.unblock, onSuccess: async () => { await client.invalidateQueries({ queryKey: ["users", "blocked"] }); await client.invalidateQueries({ queryKey: ["social-feed"] }); await client.invalidateQueries({ queryKey: ["team-directory"] }); show("User unblocked."); }, onError: error => show(error.message, "error") });
  return <main className="blocked-users-page"><header><small>PRIVACY & SAFETY</small><h1>Blocked users</h1><p>Blocked developers cannot follow you or start direct interactions. Their content is filtered from discovery and your feed.</p></header>
    {blocked.isPending ? <LoadingState label="Loading blocked users…" /> : blocked.isError ? <ErrorState message={blocked.error.message} retry={() => void blocked.refetch()} /> : blocked.data.items.length === 0 ? <EmptyState title="No blocked users" description="Developers you block will appear here so you can review or unblock them." /> : <section>{blocked.data.items.map(user => <article key={user.publicId}>{user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : <span>{user.displayName.slice(0, 2).toUpperCase()}</span>}<div><Link to={`/users/${user.publicId}`}>{user.displayName}</Link><small>@{user.userName} · blocked {new Date(user.blockedAt).toLocaleDateString()}</small></div><button disabled={unblock.isPending} onClick={() => unblock.mutate(user.publicId)}>Unblock</button></article>)}</section>}
  </main>;
}
