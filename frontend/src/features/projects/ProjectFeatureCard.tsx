import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import type { ProjectListItem } from "./types";

const formatDate = (date: string) => new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric" }).format(new Date(date));
export function deadlineLabel(deadlineAt?: string, status?: ProjectListItem["status"]) {
  if (!deadlineAt) return "No deadline";
  if (status === "Archived") return "Archived";
  if (status === "Suspended") return "Suspended";
  const today = new Date(); today.setHours(0, 0, 0, 0); const deadline = new Date(deadlineAt); deadline.setHours(0, 0, 0, 0);
  const days = Math.round((deadline.getTime() - today.getTime()) / 86_400_000);
  if (days < 0) return `Overdue by ${Math.abs(days)} day${days === -1 ? "" : "s"}`;
  if (days === 0) return "Due today";
  return `${days} day${days === 1 ? "" : "s"} remaining`;
}
const statusLabel: Record<ProjectListItem["status"], string> = { Draft: "Draft", Active: "Active", DeadlineSoon: "Due soon", DeadlineExpired: "Overdue", Suspended: "Suspended", Archived: "Archived", Deleted: "Deleted" };
interface Props { project: ProjectListItem; onEdit: (project: ProjectListItem) => void; onLifecycle: (project: ProjectListItem, action: "Active" | "Suspended" | "Archived") => void; onDelete: (project: ProjectListItem) => void; }
export function ProjectFeatureCard({ project, onEdit, onLifecycle, onDelete }: Props) {
  const navigate = useNavigate(); const [menuOpen, setMenuOpen] = useState(false); const menuRef = useRef<HTMLDivElement>(null);
  const canManage = project.currentUserRole === "Owner" || project.currentUserRole === "Admin";
  useEffect(() => { const close = (event: MouseEvent) => { if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false); }; document.addEventListener("mousedown", close); return () => document.removeEventListener("mousedown", close); }, []);
  return <article className="feature-project-card upgraded-project-card"><div className="project-card-topline"><div className="feature-project-icon">{project.name.slice(0, 2).toUpperCase()}</div><div className="project-card-actions" ref={menuRef}><span className={`status ${project.status.toLowerCase()}`}>{statusLabel[project.status]}</span>{canManage && <button type="button" className="project-menu-trigger" aria-label={`Project actions for ${project.name}`} aria-expanded={menuOpen} onClick={() => setMenuOpen((value) => !value)}>⋮</button>}{menuOpen && <div className="project-action-menu" role="menu" aria-label={`${project.name} actions`}><button role="menuitem" onClick={() => { setMenuOpen(false); onEdit(project); }}>Edit project</button><button role="menuitem" onClick={() => { setMenuOpen(false); navigate(`/projects/${project.id}/settings`); }}>Project settings</button><button role="menuitem" onClick={() => { setMenuOpen(false); navigate(`/projects/${project.id}/settings`); }}>Manage team</button>{project.status === "Suspended" || project.status === "Archived" ? <button role="menuitem" onClick={() => { setMenuOpen(false); onLifecycle(project, "Active"); }}>Resume project</button> : <button role="menuitem" onClick={() => { setMenuOpen(false); onLifecycle(project, "Suspended"); }}>Suspend project</button>}{project.status !== "Archived" && <button role="menuitem" onClick={() => { setMenuOpen(false); onLifecycle(project, "Archived"); }}>Archive project</button>}{project.currentUserRole === "Owner" && <button role="menuitem" className="danger" onClick={() => { setMenuOpen(false); onDelete(project); }}>Delete project</button>}</div>}</div></div><h2>{project.name}</h2><p>{project.description || "No description has been added yet."}</p><div className="project-deadline"><span aria-hidden="true">◷</span><div><small>Deadline {project.deadlineAt ? formatDate(project.deadlineAt) : ""}</small><strong className={project.status === "DeadlineExpired" ? "overdue-copy" : ""}>{deadlineLabel(project.deadlineAt, project.status)}</strong></div></div><div className="feature-project-meta"><span>{project.defaultLanguage}</span><span>{project.memberCount} member{project.memberCount === 1 ? "" : "s"}</span><span>{project.isReadOnly ? "Read-only" : "Writable"}</span></div><div className="project-card-footer"><span>Updated {formatDate(project.updatedAt || project.createdAt)}</span><Link to={`/projects/${project.id}/workspace`}>Open workspace <span>→</span></Link></div></article>;
}
