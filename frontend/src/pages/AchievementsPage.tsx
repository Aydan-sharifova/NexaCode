import { useQuery } from "@tanstack/react-query";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { achievementsApi } from "../features/achievements/api";
import { useAuth } from "../hooks/useAuth";

export function AchievementsPage() {
  const { session } = useAuth(); const profile = useQuery({ queryKey: ["achievements", "me"], queryFn: achievementsApi.mine }); const journey = useQuery({ queryKey: ["developer-journey", session?.user.userName], queryFn: () => achievementsApi.journey(session!.user.userName), enabled: Boolean(session) });
  if (profile.isPending) return <main className="dashboard-content"><LoadingState label="Verifying achievements…" /></main>;
  if (profile.isError) return <main className="dashboard-content"><ErrorState message={profile.error.message} retry={() => profile.refetch()} /></main>;
  const data = profile.data;
  return <main className="dashboard-content feature-page achievements-page"><header className="feature-heading"><div><p className="dashboard-date">VERIFIED DEVELOPER GROWTH</p><h1>Achievements</h1><p>Unlocked only from server-verified project, Git, review, community and AI activity.</p></div><div className="reputation-badge"><small>{data.contributionLevel}</small><strong>{data.reputationScore}</strong><span>reputation</span></div></header>
    <section className="achievement-overview"><div><strong>{data.unlockedCount}</strong><span>unlocked</span></div><div><strong>{data.totalCount - data.unlockedCount}</strong><span>in progress</span></div><div><strong>{Math.round(data.unlockedCount / Math.max(1, data.totalCount) * 100)}%</strong><span>journey complete</span></div></section>
    <section className="achievement-grid">{data.achievements.map(item => <article key={item.id} className={`achievement-card ${item.unlocked ? "unlocked" : "locked"}`}><div className="achievement-icon">{item.unlocked ? "✓" : "◇"}</div><div><span>{item.category} · {item.points} pts</span><h2>{item.title}{item.verified && <b title="Server verified">Verified</b>}</h2><p>{item.description}</p>{item.unlocked ? <small>Unlocked {new Date(item.unlockedAt!).toLocaleDateString()} · {item.evidenceType}</small> : <div className="achievement-progress"><i><em style={{ width: `${Math.min(100, item.progress / item.target * 100)}%` }} /></i><small>{item.progress} / {item.target}</small></div>}</div></article>)}</section>
    <section className="journey-panel"><header><p>DEVELOPER JOURNEY</p><h2>Verified milestones</h2></header>{journey.data?.length ? <div className="journey-line">{journey.data.map(item => <article key={`${item.code}-${item.occurredAt}`}><time>{new Date(item.occurredAt).toLocaleDateString()}</time><span /><div><h3>{item.title}</h3><p>{item.description}</p></div></article>)}</div> : <p className="marketplace-state">Your verified milestones will appear here.</p>}</section></main>;
}
