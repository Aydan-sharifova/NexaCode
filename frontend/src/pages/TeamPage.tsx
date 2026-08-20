import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { projectApi } from "../features/projects/api";
import type { ProjectRole } from "../features/projects/types";

interface TeamMember { userId: string; publicId?: string; fullName: string; email: string; avatarUrl?: string; projects: Array<{ id: string; name: string; role: ProjectRole }>; }
async function loadTeam(): Promise<TeamMember[]> {
  const projects = await projectApi.list(); const membership = await Promise.all(projects.map(async (project) => ({ project, members: await projectApi.members(project.id) }))); const users = new Map<string, TeamMember>();
  membership.forEach(({ project, members }) => members.forEach((member) => { const current = users.get(member.userId) ?? { userId: member.userId, publicId: member.publicId, fullName: member.fullName, email: member.email, avatarUrl: member.avatarUrl, projects: [] }; current.projects.push({ id: project.id, name: project.name, role: member.role }); users.set(member.userId, current); }));
  return [...users.values()].sort((a, b) => a.fullName.localeCompare(b.fullName));
}
export function TeamPage() {
  const team = useQuery({ queryKey: ["team-directory"], queryFn: loadTeam, staleTime: 60_000 }); const [search, setSearch] = useState(""); const filtered = useMemo(() => team.data?.filter((member) => `${member.fullName} ${member.email} ${member.projects.map((x) => x.name).join(" ")}`.toLowerCase().includes(search.toLowerCase())) ?? [], [team.data, search]);
  return <main className="dashboard-content feature-page"><header className="feature-heading"><div><p className="dashboard-date">PEOPLE</p><h1>Team directory</h1><p>Members from every project you can access.</p></div><label className="page-search"><span>⌕</span><input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search people or projects…" /></label></header>{team.isLoading ? <LoadingState label="Loading team members…" /> : team.isError ? <ErrorState message={team.error.message} retry={() => team.refetch()} /> : filtered.length ? <section className="team-grid">{filtered.map((member) => <article key={member.userId}><span className="team-avatar">{member.fullName.split(" ").map((part) => part[0]).join("").slice(0, 2)}</span><h2><Link to={`/users/${encodeURIComponent(member.publicId || member.userId)}`}>{member.fullName}</Link></h2><p>{member.email}</p><div>{member.projects.map((project) => <span key={project.id}><b>{project.name}</b><small>{project.role}</small></span>)}</div></article>)}</section> : <EmptyState title="No team members found" description={search ? "Try a different search." : "Join or create a project to build your team directory."} />}</main>;
}
