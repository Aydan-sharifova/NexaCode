import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Icon } from "../components/Icon";
import { mentorApi, mentorKeys } from "../features/mentor/api";
import { useToast } from "../contexts/ToastContext";

const categoryLabels: Record<string, string> = { NextTechnology: "Next technology", ProjectIdea: "Project idea", MissingSkill: "Missing skill", TestingImprovement: "Testing improvement", ArchitectureTopic: "Architecture topic" };

export function MentorPage() {
  const queryClient = useQueryClient(); const { show } = useToast();
  const analysis = useQuery({ queryKey: mentorKeys.analysis, queryFn: mentorApi.analysis });
  const generate = useMutation({ mutationFn: mentorApi.generate, onSuccess: value => queryClient.setQueryData(mentorKeys.analysis, value), onError: error => show(error.message, "error") });
  if (analysis.isLoading) return <main className="mentor-page"><div className="route-loader">Analyzing authorized development evidence…</div></main>;
  if (analysis.isError || !analysis.data) return <main className="mentor-page"><div className="mentor-empty"><h1>Mentor analysis unavailable</h1><p>{analysis.error?.message ?? "Please try again."}</p><button onClick={() => void analysis.refetch()}>Retry</button></div></main>;
  const data = analysis.data; const evidence = data.evidence;
  return <main className="mentor-page">
    <header className="mentor-hero"><div><span className="mentor-eyebrow">PRIVATE · EVIDENCE-BASED</span><h1>Your AI Personal Mentor</h1><p>Recommendations grounded in work you can access—never inferred personal traits.</p></div><button className="ui-button primary" disabled={generate.isPending} onClick={() => generate.mutate()}><Icon name="activity" />{generate.isPending ? "Asking Ollama…" : "Generate with Ollama"}</button></header>
    <section className="mentor-evidence" aria-label="Evidence used"><article><b>{evidence.projectCount}</b><span>Projects</span></article><article><b>{evidence.completedTaskCount}</b><span>Completed tasks</span></article><article><b>{evidence.commitCount}</b><span>Commits</span></article><article><b>{evidence.testFileCount}</b><span>Test files</span></article></section>
    <section className="mentor-data"><div><h2>Declared skills</h2><div className="mentor-tags">{evidence.declaredSkills.length ? evidence.declaredSkills.map(x => <span key={x}>{x}</span>) : <small>Add skills in Settings → Profile.</small>}</div></div><div><h2>Observed technologies</h2><div className="mentor-tags">{evidence.observedTechnologies.length ? evidence.observedTechnologies.map(x => <span key={x}>{x}</span>) : <small>No project-language evidence yet.</small>}</div></div><div><h2>Learning topics</h2><div className="mentor-tags">{evidence.learningTopics.length ? evidence.learningTopics.map(x => <span key={x}>{x}</span>) : <small>Add topics you want to learn in Settings.</small>}</div></div></section>
    {data.modelNarrative && <section className="mentor-narrative"><header><span className="mentor-orb">AI</span><div><h2>Ollama mentor summary</h2><small>{data.model}</small></div></header><p>{data.modelNarrative}</p></section>}
    {!data.modelAvailable && <div className="mentor-model-state"><Icon name="help" /><span>The evidence engine is ready. Generate when the configured local Ollama model is available; no fabricated AI response is shown.</span></div>}
    <section className="mentor-recommendations">{data.recommendations.map((item, index) => <article key={item.category}><span className="mentor-number">0{index + 1}</span><small>{categoryLabels[item.category] ?? item.category}</small><h2>{item.title}</h2><p>{item.rationale}</p><div><b>Next action</b><span>{item.action}</span></div></article>)}</section>
    <footer className="mentor-privacy"><Icon name="check" /><span>{data.privacyNotice}</span><time>{new Date(evidence.analyzedAt).toLocaleString()}</time></footer>
  </main>;
}
