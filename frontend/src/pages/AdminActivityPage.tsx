import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { activityApi, isGuid, type ActivityFilters } from "../features/activities/api";
import { usePageTranslation } from "../hooks/usePageTranslation";

export function AdminActivityPage() {
  const { pt, locale } = usePageTranslation();
  const [filters, setFilters] = useState<ActivityFilters>({ page: 1 });
  const [userId, setUserId] = useState("");
  const [projectId, setProjectId] = useState("");
  const invalidUserId = userId.trim().length > 0 && !isGuid(userId);
  const invalidProjectId = projectId.trim().length > 0 && !isGuid(projectId);
  const logs = useQuery({ queryKey: ["admin", "activities", filters], queryFn: () => activityApi.list(filters) });

  const displayAction = (value: string) => value === "Login" ? pt("login") : value === "Logout" ? pt("logout") : value;
  const displayEntity = (value: string) => value === "User" ? pt("user") : value;
  const displayDescription = (value: string) => value === "User signed in." ? pt("signedIn") : value === "User signed out." ? pt("signedOut") : value;
  const update = (key: keyof ActivityFilters, value: string) => setFilters((old) => ({ ...old, [key]: value || undefined, page: 1 }));
  const updateId = (key: "userId" | "projectId", value: string) => {
    key === "userId" ? setUserId(value) : setProjectId(value);
    setFilters((old) => ({ ...old, [key]: isGuid(value) ? value.trim() : undefined, page: 1 }));
  };

  return <main className="dashboard-content activity-page">
    <header className="feature-heading"><div><p className="dashboard-date">{pt("adminAudit")}</p><h1>{pt("activityLog")}</h1><p>{pt("auditCopy")}</p></div></header>
    <section className="activity-filters">
      <input placeholder={pt("userId")} value={userId} aria-invalid={invalidUserId} aria-describedby={invalidUserId ? "activity-id-error" : undefined} onChange={(e) => updateId("userId", e.target.value)} />
      <input placeholder={pt("projectId")} value={projectId} aria-invalid={invalidProjectId} aria-describedby={invalidProjectId ? "activity-id-error" : undefined} onChange={(e) => updateId("projectId", e.target.value)} />
      <input placeholder={pt("actionType")} onChange={(e) => update("actionType", e.target.value)} />
      <input placeholder={pt("entityType")} onChange={(e) => update("entityType", e.target.value)} />
      <input type="date" aria-label={pt("fromDate")} onChange={(e) => update("from", e.target.value)} />
      <input type="date" aria-label={pt("toDate")} onChange={(e) => update("to", e.target.value)} />
      {(invalidUserId || invalidProjectId) && <span className="activity-filter-error" id="activity-id-error" role="alert">{pt("fullUuidRequired")}</span>}
    </section>
    {logs.isLoading ? <LoadingState label={pt("loadingActivity")} /> : logs.isError ? <ErrorState message={logs.error.message} retry={() => logs.refetch()} /> : !logs.data?.items.length ? <EmptyState title={pt("noMatching")} description={pt("filtersCopy")} /> : <>
      <section className="activity-table-wrap"><table className="activity-table"><thead><tr><th>{pt("when")}</th><th>{pt("user")}</th><th>{pt("project")}</th><th>{pt("actionLabel")}</th><th>{pt("entity")}</th><th>{pt("description")}</th><th>IP</th></tr></thead><tbody>{logs.data.items.map((item) => <tr key={item.id}><td>{new Date(item.createdAt).toLocaleString(locale)}</td><td>{item.userName ?? item.userId ?? pt("system")}</td><td>{item.projectName ?? item.projectId ?? "—"}</td><td><span>{displayAction(item.actionType)}</span></td><td>{displayEntity(item.entityType)}</td><td>{displayDescription(item.description)}</td><td>{item.ipAddress ?? "—"}</td></tr>)}</tbody></table></section>
      <div className="activity-pagination"><button disabled={logs.data.page <= 1} onClick={() => setFilters((x) => ({ ...x, page: (x.page ?? 1) - 1 }))}>{pt("previous")}</button><span>{pt("page")} {logs.data.page} · {logs.data.total} {pt("events")}</span><button disabled={logs.data.page * logs.data.pageSize >= logs.data.total} onClick={() => setFilters((x) => ({ ...x, page: (x.page ?? 1) + 1 }))}>{pt("next")}</button></div>
    </>}
  </main>;
}
