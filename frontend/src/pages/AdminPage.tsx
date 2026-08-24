import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ConfirmDialog } from "../components/ui/Dialog";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { adminApi, type AdminUser, type AdminUserDetails, type ProgrammingLanguage } from "../features/admin/api";
import { useAuth } from "../hooks/useAuth";
import { useToast } from "../contexts/ToastContext";
import { usePageTranslation } from "../hooks/usePageTranslation";
import { queryKeys } from "../services/queryKeys";

type Confirmation = { kind: "suspend" | "delete-user" | "delete-project"; id: string; active?: boolean };
export function AdminPage() {
  const { pt, locale } = usePageTranslation();
  const [tab, setTab] = useState<"users" | "projects" | "languages">("users");
  const [search, setSearch] = useState(""); const [term, setTerm] = useState(""); const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string>(); const [editing, setEditing] = useState(false); const [confirm, setConfirm] = useState<Confirmation>();
  const qc = useQueryClient(); const { session } = useAuth(); const { show } = useToast();
  const superAdmin = session?.user.roles.includes("SuperAdmin") ?? false;
  useEffect(() => { const timer = window.setTimeout(() => { setTerm(search); setPage(1); }, 300); return () => clearTimeout(timer); }, [search]);
  const stats = useQuery({ queryKey: ["admin", "stats"], queryFn: adminApi.stats });
  const users = useQuery({ queryKey: ["admin", "users", term, page], queryFn: () => adminApi.users(term, page), enabled: tab === "users" });
  const projects = useQuery({ queryKey: ["admin", "projects", term, page], queryFn: () => adminApi.projects(term, page), enabled: tab === "projects" });
  const languages = useQuery({ queryKey: ["admin", "languages"], queryFn: adminApi.languages, enabled: tab === "languages" });
  const details = useQuery({ queryKey: ["admin", "user", selected], queryFn: () => adminApi.user(selected!), enabled: Boolean(selected) });
  const refresh = () => { void qc.invalidateQueries({ queryKey: ["admin"] }); };
  const role = useMutation({ mutationFn: ({ userId, name, enabled }: { userId: string; name: string; enabled: boolean }) => adminApi.role(userId, name, enabled), onSuccess: () => { refresh(); show("System rolu yeniləndi."); }, onError: (error) => show(error.message, "error") });
  const update = useMutation({ mutationFn: (value: UserEdit) => adminApi.updateUser(selected!, value), onSuccess: () => { setEditing(false); refresh(); show("İstifadəçi məlumatları yeniləndi."); }, onError: (error) => show(error.message, "error") });
  const action = useMutation({
    mutationFn: async () => {
      if (!confirm) return;
      if (confirm.kind === "suspend") {
        const suspending = confirm.active === true;
        const reason = suspending ? prompt("Bloklama səbəbi")?.trim() : undefined;
        if (suspending && !reason) throw new Error("Bloklama səbəbi mütləqdir.");
        const duration = suspending ? prompt("Müddət: 1h, 24h, 3d, 7d, 30d və ya permanent", "24h")?.trim().toLowerCase() : undefined;
        const durations: Record<string, number> = { "1h": 1, "24h": 24, "3d": 72, "7d": 168, "30d": 720 };
        if (suspending && duration !== "permanent" && !durations[duration ?? ""]) throw new Error("Müddət 1h, 24h, 3d, 7d, 30d və ya permanent olmalıdır.");
        const expiresAt = suspending && duration !== "permanent" ? new Date(Date.now() + durations[duration!] * 3_600_000).toISOString() : undefined;
        await adminApi.suspension(confirm.id, suspending, reason, expiresAt);
      }
      if (confirm.kind === "delete-user") { const reason = prompt("İstifadəçinin silinmə səbəbi"); if (!reason) throw new Error("Səbəb mütləqdir."); await adminApi.deleteUser(confirm.id, reason); }
      if (confirm.kind === "delete-project") { const reason = prompt("Layihənin silinmə səbəbi"); if (!reason) throw new Error("Səbəb mütləqdir."); await adminApi.deleteProject(confirm.id, reason); }
    },
    onSuccess: () => {
      if (confirm?.kind === "delete-user") {
        setSelected(undefined);
        void qc.invalidateQueries({ queryKey: queryKeys.teamDirectory });
        void qc.invalidateQueries({ queryKey: queryKeys.projects });
        void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
      }
      setConfirm(undefined);
      refresh();
      show("Əməliyyat uğurla tamamlandı.");
    },
    onError: (error) => show(error.message, "error"),
  });
  const data = tab === "users" ? users.data : tab === "projects" ? projects.data : undefined; const loading = tab === "users" ? users.isLoading : tab === "projects" ? projects.isLoading : languages.isLoading; const error = tab === "users" ? users.error : tab === "projects" ? projects.error : languages.error;
  return <main className="admin-page">
    <header className="admin-heading"><div><p>{pt("platformControl")}</p><h1>{pt("administration")}</h1><span>{pt("adminCopy")}</span></div><a href="/admin/activity">{pt("openAudit")}</a></header>
    <section className="admin-stats">{stats.data && Object.entries(stats.data).map(([key, value]) => <article key={key}><small>{pt(key as "totalUsers"|"activeUsers30Days"|"suspendedUsers"|"totalProjects"|"projects30Days"|"activity30Days")}</small><strong>{value}</strong></article>)}</section>
    <div className="admin-tabs"><button className={tab === "users" ? "active" : ""} onClick={() => { setTab("users"); setPage(1); }}>{pt("users")}</button><button className={tab === "projects" ? "active" : ""} onClick={() => { setTab("projects"); setPage(1); }}>{pt("projects")}</button><button className={tab === "languages" ? "active" : ""} onClick={() => setTab("languages")}>Programming languages</button>{tab !== "languages" && <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={`${pt(tab)}…`} />}</div>
    {loading ? <LoadingState label={pt("loadingData")} /> : error ? <ErrorState message={error.message} retry={() => tab === "users" ? users.refetch() : tab === "projects" ? projects.refetch() : languages.refetch()} /> : tab === "languages" ? <LanguageCatalog languages={languages.data ?? []} refresh={refresh} /> : <section className="admin-table-wrap"><table><thead><tr>{tab === "users" ? <><th>{pt("user")}</th><th>{pt("roles")}</th><th>{pt("status")}</th><th>{pt("lastActive")}</th><th /></> : <><th>{pt("project")}</th><th>{pt("owner")}</th><th>{pt("members")}</th><th>{pt("tasks")}</th><th /></>}</tr></thead><tbody>{tab === "users" ? users.data?.items.map((user) => <UserRow key={user.id} user={user} locale={locale} onSelect={() => { setSelected(user.id); setEditing(false); }} onStatus={() => setConfirm({ kind: "suspend", id: user.id, active: !user.isSuspended })} />) : projects.data?.items.map((project) => <tr key={project.id}><td><b>{project.name}</b><small>{project.isPublic ? pt("public") : pt("private")}</small></td><td>{project.ownerName}</td><td>{project.memberCount}</td><td>{project.taskCount}</td><td><button className="danger-link" onClick={() => setConfirm({ kind: "delete-project", id: project.id })}>{pt("delete")}</button></td></tr>)}</tbody></table></section>}
    {data && <div className="admin-pagination"><button disabled={page === 1} onClick={() => setPage((value) => value - 1)}>{pt("previous")}</button><span>{pt("page")} {page} · {data.total} {pt("records")}</span><button disabled={page * data.pageSize >= data.total} onClick={() => setPage((value) => value + 1)}>{pt("next")}</button></div>}
    {selected && <div className="admin-drawer-backdrop" onClick={() => setSelected(undefined)}><aside className="admin-drawer" onClick={(event) => event.stopPropagation()}><button onClick={() => setSelected(undefined)}>×</button>{details.isLoading ? <LoadingState label="İstifadəçi yüklənir…" /> : details.data && (editing ? <UserEditForm user={details.data} pending={update.isPending} onCancel={() => setEditing(false)} onSave={(value) => update.mutate(value)} /> : <UserDetails user={details.data} superAdmin={superAdmin} onEdit={() => setEditing(true)} onDelete={() => setConfirm({ kind: "delete-user", id: details.data!.id })} onRole={(name, enabled) => role.mutate({ userId: details.data!.id, name, enabled })} />)}</aside></div>}
    <ConfirmDialog open={Boolean(confirm)} title={confirm?.kind === "delete-user" ? "İstifadəçi silinsin?" : confirm?.kind === "delete-project" ? "Layihə silinsin?" : confirm?.active ? "İstifadəçi bloklansın?" : "İstifadəçi aktivləşdirilsin?"} description="Bu əməliyyat backend tərəfindən yoxlanılır və audit jurnalına yazılır." destructive={confirm?.kind !== "suspend" || confirm?.active} confirmLabel="Təsdiqlə" onClose={() => setConfirm(undefined)} onConfirm={() => action.mutate()} />
  </main>;
}
type UserEdit = { firstName: string; lastName: string; userName: string; email: string; bio?: string };
function UserEditForm({ user, pending, onCancel, onSave }: { user: AdminUserDetails; pending: boolean; onCancel: () => void; onSave: (value: UserEdit) => void }) {
  const [value, setValue] = useState<UserEdit>({ firstName: user.firstName, lastName: user.lastName, userName: user.userName, email: user.email, bio: user.bio ?? "" });
  const field = (key: keyof UserEdit, next: string) => setValue((current) => ({ ...current, [key]: next }));
  return <form className="admin-edit-form" onSubmit={(event) => { event.preventDefault(); onSave(value); }}><h2>İstifadəçini dəyiş</h2><label>Ad<input required value={value.firstName} onChange={(event) => field("firstName", event.target.value)} /></label><label>Soyad<input required value={value.lastName} onChange={(event) => field("lastName", event.target.value)} /></label><label>Username<input required value={value.userName} onChange={(event) => field("userName", event.target.value)} /></label><label>Email<input required type="email" value={value.email} onChange={(event) => field("email", event.target.value)} /></label><label>Bio<textarea rows={4} value={value.bio} onChange={(event) => field("bio", event.target.value)} /></label><div><button type="button" onClick={onCancel}>Cancel</button><button className="ui-button primary" disabled={pending}>Save</button></div></form>;
}
function UserDetails({ user, superAdmin, onEdit, onDelete, onRole }: { user: AdminUserDetails; superAdmin: boolean; onEdit: () => void; onDelete: () => void; onRole: (role: string, enabled: boolean) => void }) {
  return <><div className="drawer-avatar">{user.firstName.slice(0, 1)}</div><h2>{user.firstName} {user.lastName}</h2><p>@{user.userName} · {user.email}</p><dl><div><dt>Projects</dt><dd>{user.projectCount}</dd></div><div><dt>Last seen</dt><dd>{new Date(user.lastSeen).toLocaleString()}</dd></div><div><dt>Status</dt><dd>{user.isSuspended ? user.banExpiresAt ? `Suspended until ${new Date(user.banExpiresAt).toLocaleString()}` : "Suspended permanently" : "Active"}</dd></div></dl>{superAdmin && <><section><h3>System roles</h3>{["SuperAdmin", "Admin", "Moderator", "User"].map((name) => <label key={name}>{name}<input type="checkbox" checked={user.roles.includes(name)} onChange={(event) => onRole(name, event.target.checked)} /></label>)}</section><div className="drawer-admin-actions"><button onClick={onEdit}>İstifadəçini dəyiş</button><button className="ui-button danger" onClick={onDelete}>İstifadəçini sil</button></div></>}</>;
}
function UserRow({ user, locale, onSelect, onStatus }: { user: AdminUser; locale:string; onSelect: () => void; onStatus: () => void }) { const {pt}=usePageTranslation(); return <tr><td><button className="admin-user-link" onClick={onSelect}><b>{user.displayName}</b><small>@{user.userName} · {user.email}</small></button></td><td>{user.roles.map((role) => <span className="role-pill" key={role}>{role}</span>)}</td><td><span className={user.isSuspended ? "status suspended" : "status active"}>{user.isSuspended ? pt("suspended") : pt("active")}</span></td><td>{new Date(user.lastSeen).toLocaleDateString(locale)}</td><td><button onClick={onStatus}>{user.isSuspended ? pt("activate") : pt("suspend")}</button></td></tr>; }

function LanguageCatalog({ languages, refresh }: { languages: ProgrammingLanguage[]; refresh: () => void }) {
  const { show } = useToast();
  const [name, setName] = useState(""); const [slug, setSlug] = useState(""); const [sortOrder, setSortOrder] = useState(100);
  const create = useMutation({ mutationFn: adminApi.createLanguage, onSuccess: () => { setName(""); setSlug(""); refresh(); show("Programming language added."); }, onError: (error) => show(error.message, "error") });
  const update = useMutation({ mutationFn: ({ id, value }: { id: string; value: { name: string; slug?: string; sortOrder: number; isActive: boolean } }) => adminApi.updateLanguage(id, value), onSuccess: () => { refresh(); show("Programming language updated."); }, onError: (error) => show(error.message, "error") });
  const remove = useMutation({ mutationFn: adminApi.deleteLanguage, onSuccess: () => { refresh(); show("Programming language removed."); }, onError: (error) => show(error.message, "error") });
  return <section className="language-catalog">
    <form onSubmit={(event) => { event.preventDefault(); create.mutate({ name, slug: slug || undefined, sortOrder }); }}><label>Name<input required maxLength={50} value={name} onChange={(event) => setName(event.target.value)} placeholder="e.g. Rust" /></label><label>Slug (optional)<input maxLength={50} value={slug} onChange={(event) => setSlug(event.target.value)} placeholder="rust" /></label><label>Order<input type="number" value={sortOrder} onChange={(event) => setSortOrder(Number(event.target.value))} /></label><button className="ui-button primary" disabled={create.isPending}>Add language</button></form>
    <div className="admin-table-wrap"><table><thead><tr><th>Name</th><th>Slug</th><th>Order</th><th>Status</th><th /></tr></thead><tbody>{languages.map((language) => <LanguageRow key={language.id} language={language} pending={update.isPending || remove.isPending} onSave={(value) => update.mutate({ id: language.id, value })} onDelete={() => { if (window.confirm(`Remove ${language.name}? Existing projects keep their saved language.`)) remove.mutate(language.id); }} />)}</tbody></table></div>
  </section>;
}

function LanguageRow({ language, pending, onSave, onDelete }: { language: ProgrammingLanguage; pending: boolean; onSave: (value: { name: string; slug?: string; sortOrder: number; isActive: boolean }) => void; onDelete: () => void }) {
  const [value, setValue] = useState(language);
  useEffect(() => setValue(language), [language]);
  return <tr><td><input aria-label="Language name" value={value.name} onChange={(event) => setValue({ ...value, name: event.target.value })} /></td><td><input aria-label="Language slug" value={value.slug} onChange={(event) => setValue({ ...value, slug: event.target.value })} /></td><td><input aria-label="Language order" type="number" value={value.sortOrder} onChange={(event) => setValue({ ...value, sortOrder: Number(event.target.value) })} /></td><td><label className="language-active"><input type="checkbox" checked={value.isActive} onChange={(event) => setValue({ ...value, isActive: event.target.checked })} /> {value.isActive ? "Active" : "Inactive"}</label></td><td><button disabled={pending} onClick={() => onSave(value)}>Save</button><button className="danger-link" disabled={pending} onClick={onDelete}>Delete</button></td></tr>;
}
