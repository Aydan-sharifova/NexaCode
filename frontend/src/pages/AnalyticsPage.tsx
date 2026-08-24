import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { analyticsApi } from "../features/analytics/api";
import { queryKeys } from "../services/queryKeys";

const colors = ["#6c5ce7", "#3182f6", "#13b981", "#f59e0b", "#ef476f", "#06b6d4", "#8b5cf6", "#84cc16"];
const ranges = { "7d": 7, "30d": 30, "90d": 90, "1y": 365 } as const;
export function AnalyticsPage() {
  const [range, setRange] = useState<keyof typeof ranges>("30d");
  const dates = useMemo(() => { const to = new Date(), from = new Date(to); from.setDate(from.getDate() - ranges[range]); return { from: from.toISOString(), to: to.toISOString() }; }, [range]);
  const analytics = useQuery({ queryKey: [...queryKeys.analytics, range], queryFn: () => analyticsApi.dashboard(dates.from, dates.to), staleTime: 30_000 });
  if (analytics.isLoading) return <main className="analytics-page"><LoadingState label="Aggregating analytics…" /></main>;
  if (analytics.isError) return <main className="analytics-page"><ErrorState message={analytics.error.message} retry={() => analytics.refetch()} /></main>;
  const data = analytics.data!;
  const cards = [["Active users", data.summary.activeUsers], ["Projects created", data.summary.projectsCreated], ["Task completion", `${data.summary.taskCompletionRate}%`], ["File changes", data.summary.fileChanges], ["Coding time", `${data.summary.estimatedCodingHours}h`]];
  const developerCards=Object.entries(data.developer).map(([key,value])=>[key.replace(/([A-Z])/g," $1").replace(/^./,x=>x.toUpperCase()),value]);
  return <main className="analytics-page">
    <header className="analytics-heading"><div><p>WORKSPACE INTELLIGENCE</p><h1>Analytics</h1><span>Aggregated usage across projects you can access.</span></div><div className="range-selector">{Object.keys(ranges).map((key) => <button className={range === key ? "active" : ""} onClick={() => setRange(key as keyof typeof ranges)} key={key}>{key.toUpperCase()}</button>)}</div></header>
    <section className="analytics-summary">{cards.map(([label, value]) => <article key={label}><small>{label}</small><strong>{value}</strong></article>)}</section>
    <section className="analytics-card wide"><header><h2>Developer analytics</h2><p>Your verified activity within accessible projects for this period</p></header><div className="analytics-summary">{developerCards.map(([label,value])=><article key={label}><small>{label}</small><strong>{value}</strong></article>)}</div></section>
    <section className="analytics-grid">
      <article className="analytics-card wide"><header><h2>Weekly activity</h2><p>Meaningful workspace actions—not keystrokes</p></header>{data.weeklyActivity.length ? <ResponsiveContainer width="100%" height={280}><AreaChart data={data.weeklyActivity}><defs><linearGradient id="activityFill" x1="0" y1="0" x2="0" y2="1"><stop offset="5%" stopColor="#6c5ce7" stopOpacity={0.38}/><stop offset="95%" stopColor="#6c5ce7" stopOpacity={0}/></linearGradient></defs><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey="period" tickFormatter={(value) => new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" })}/><YAxis allowDecimals={false}/><Tooltip/><Area type="monotone" dataKey="value" stroke="#6c5ce7" fill="url(#activityFill)" strokeWidth={3}/></AreaChart></ResponsiveContainer> : <Empty label="No activity in this period." />}</article>
      <article className="analytics-card"><header><h2>Languages</h2><p>Most used project languages</p></header>{data.languages.length ? <ResponsiveContainer width="100%" height={260}><PieChart><Pie data={data.languages} dataKey="projectCount" nameKey="language" innerRadius={58} outerRadius={90} paddingAngle={3}>{data.languages.map((item, index) => <Cell key={item.language} fill={colors[index % colors.length]}/>)}</Pie><Tooltip/></PieChart></ResponsiveContainer> : <Empty label="No language data yet." />}<div className="chart-legend">{data.languages.map((item, index) => <span key={item.language}><i style={{ background: colors[index % colors.length] }}/>{item.language} <b>{item.projectCount}</b></span>)}</div></article>
      <article className="analytics-card"><header><h2>Most active users</h2><p>Ranked by recorded activity</p></header><div className="active-users">{data.activeUsers.length ? data.activeUsers.map((user, index) => <div key={user.userId}><span>{index + 1}</span><i>{user.displayName.slice(0, 1)}</i><p><b>{user.displayName}</b><small>@{user.userName}</small></p><strong>{user.activityCount}</strong></div>) : <Empty label="No active users yet." />}</div></article>
      <article className="analytics-card wide"><header><h2>Projects created</h2><p>New accessible projects over time</p></header>{data.projectsOverTime.length ? <ResponsiveContainer width="100%" height={260}><BarChart data={data.projectsOverTime}><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey="period" tickFormatter={(value) => new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" })}/><YAxis allowDecimals={false}/><Tooltip/><Bar dataKey="value" fill="#3182f6" radius={[7, 7, 0, 0]}/></BarChart></ResponsiveContainer> : <Empty label="No projects created in this period." />}</article>
    </section>
    <section className="analytics-card wide"><header><h2>Project analytics</h2><p>Private projects appear only when you are a member. Views are unique per viewer/day.</p></header><div className="analytics-project-table"><div><b>Project</b><b>Views</b><b>Likes</b><b>Saves</b><b>Contributors</b><b>Deployments</b><b>Activity</b></div>{data.projects.map(project=><div key={project.projectId}><strong>{project.name}<small>{project.isPublic?"Public":"Private"}</small></strong><span>{project.views}</span><span>{project.likes}</span><span>{project.saves}</span><span>{project.contributors}</span><span>{project.deployments}</span><span>{project.activity}</span></div>)}</div><small>Fork analytics: unavailable until a real repository fork workflow is implemented.</small></section>
  </main>;
}
function Empty({ label }: { label: string }) { return <div className="analytics-empty">{label}</div>; }
