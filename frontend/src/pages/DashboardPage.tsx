import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { Icon, type IconName } from "../components/Icon";
import { ProjectCard } from "../components/ProjectCard";
import { StatCard } from "../components/StatCard";
import { WeeklyProgressChart } from "../components/WeeklyProgressChart";
import { useAuth } from "../hooks/useAuth";
import {
  formatDashboardDate,
  usePageTranslation,
} from "../hooks/usePageTranslation";
import { dashboardApi } from "../services/dashboardApi";
import type { DashboardProject, ProjectSummary } from "../types/dashboard";
import { queryKeys } from "../services/queryKeys";

const colors = [
  "#6c5ce7",
  "#3182f6",
  "#13b981",
  "#f59e0b",
  "#ef476f",
  "#06b6d4",
];
const metricStyle: Record<string, { icon: IconName; tone: string }> = {
  projects: { icon: "folder", tone: "purple" },
  saves: { icon: "code", tone: "blue" },
  completion: { icon: "activity", tone: "green" },
  members: { icon: "team", tone: "orange" },
};
const relative = (date: string) => {
  const seconds = Math.max(
    0,
    Math.floor((Date.now() - new Date(date).getTime()) / 1000),
  );
  if (seconds < 60) return "just now";
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
  return `${Math.floor(seconds / 86400)}d ago`;
};
const summary = (project: DashboardProject, index: number): ProjectSummary => ({
  id: project.id,
  name: project.name,
  description:
    project.description ||
    `${project.memberCount} members · ${project.openTaskCount} open tasks`,
  language: project.language,
  progress: project.progress,
  updatedAt: relative(project.updatedAt),
  color: colors[index % colors.length],
});

export function DashboardPage() {
  const dashboard = useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: dashboardApi.get,
    staleTime: 30_000,
  });
  const { session } = useAuth();
  const navigate = useNavigate();
  const { pt, language, locale } = usePageTranslation();
  const hour = new Date().getHours();
  const greeting =
    hour < 12 ? pt("morning") : hour < 18 ? pt("afternoon") : pt("evening");
  if (dashboard.isLoading)
    return (
      <main className="dashboard-content">
        <LoadingState label={pt("loadingAnalytics")} />
      </main>
    );
  if (dashboard.isError)
    return (
      <main className="dashboard-content">
        <ErrorState
          message={dashboard.error.message}
          retry={() => dashboard.refetch()}
        />
      </main>
    );
  const data = dashboard.data!;
  const metricLabels: Record<string, string> = {
    projects: pt("activeProjects"),
    saves: pt("savesWeek"),
    completion: pt("taskCompletion"),
    members: pt("teamMembers"),
  };
  return (
    <main className="dashboard-content">
      <header className="dashboard-heading">
        <div>
          <p className="dashboard-date">
            {formatDashboardDate(new Date(), language).toLocaleUpperCase(locale)}
          </p>
          <h1>
            {greeting}, {session?.user.firstName} <span>👋</span>
          </h1>
          <p>{pt("dashboardCopy")}</p>
        </div>
        <button
          className="secondary-button"
          onClick={() =>
            navigate(
              session?.user.roles.includes("Admin")
                ? "/admin/activity"
                : "/projects",
            )
          }
        >
          <Icon name="chart" /> {pt("viewReport")}
        </button>
      </header>
      <section className="stats-grid" aria-label="Workspace statistics">
        {data.metrics.map((metric) => {
          const style = metricStyle[metric.key] ?? {
            icon: "chart" as IconName,
            tone: "purple",
          };
          return (
            <StatCard
              key={metric.key}
              label={metricLabels[metric.key] ?? metric.label}
              value={metric.displayValue}
              change={
                metric.key === "members"
                  ? pt("acrossProjects")
                  : pt("fromLastWeek")
              }
              changePercent={metric.changePercent}
              {...style}
            />
          );
        })}
      </section>
      <section className="dashboard-grid">
        <article className="panel progress-panel">
          <div className="panel-heading">
            <div>
              <h2>{pt("weeklyProgress")}</h2>
              <p>{pt("weeklyCopy")}</p>
            </div>
            <span className="live-badge">{pt("live")}</span>
          </div>
          <WeeklyProgressChart points={data.weeklyProgress} />
        </article>
        <article className="panel activity-panel">
          <div className="panel-heading">
            <div>
              <h2>{pt("recentActivity")}</h2>
              <p>{pt("recentCopy")}</p>
            </div>
            {session?.user.roles.includes("Admin") && (
              <button onClick={() => navigate("/admin/activity")}>
                {pt("viewAll")}
              </button>
            )}
          </div>
          <div className="activity-list">
            {data.recentActivity.length ? (
              data.recentActivity.map((activity, index) => (
                <div className="activity-item" key={activity.id}>
                  <span
                    className={`activity-icon ${["purple", "blue", "green", "orange"][index % 4]}`}
                  >
                    <Icon
                      name={
                        activity.entityType.includes("Project")
                          ? "folder"
                          : activity.entityType.includes("Task")
                            ? "activity"
                            : "code"
                      }
                    />
                  </span>
                  <div>
                    <strong>{activity.description}</strong>
                    <p>
                      {activity.userName ?? "System"}
                      {activity.projectName ? ` · ${activity.projectName}` : ""}
                    </p>
                  </div>
                  <time>{relative(activity.createdAt)}</time>
                </div>
              ))
            ) : (
              <p className="dashboard-empty-copy">{pt("noActivity")}</p>
            )}
          </div>
        </article>
      </section>
      <section className="projects-section">
        <div className="section-heading">
          <div>
            <h2>{pt("projects")}</h2>
            <p>{pt("continue")}</p>
          </div>
          <button onClick={() => navigate("/projects")}>
            {pt("viewAllProjects")} <Icon name="chevron" />
          </button>
        </div>
        {data.projects.length ? (
          <div className="projects-grid">
            {data.projects.map((project, index) => (
              <ProjectCard key={project.id} project={summary(project, index)} />
            ))}
          </div>
        ) : (
          <div className="feature-state">
            <strong>{pt("noProjects")}</strong>
            <p>{pt("dashboardNoProjectsCopy")}</p>
            <button
              className="ui-button primary"
              onClick={() => navigate("/projects")}
            >
              {pt("goProjects")}
            </button>
          </div>
        )}
      </section>
    </main>
  );
}
