import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { projectPlannerApi, plannerKeys, type ProjectPlanDetails } from "../features/project-planner/api";
import { useToast } from "../contexts/ToastContext";

const sectionLabels: Record<string, string> = { architecture: "Architecture", database: "Database", api: "API", frontend: "Frontend", authentication: "Authentication", testing: "Testing", deployment: "Deployment" };

export function ProjectPlannerPage() {
  const [idea, setIdea] = useState(""); const [selectedId, setSelectedId] = useState<string>(); const client = useQueryClient(); const navigate = useNavigate(); const { show } = useToast();
  const plans = useQuery({ queryKey: plannerKeys.all, queryFn: projectPlannerApi.list });
  const selected = useQuery({ queryKey: plannerKeys.detail(selectedId ?? ""), queryFn: () => projectPlannerApi.get(selectedId!), enabled: Boolean(selectedId) });
  const setPlan = (value: ProjectPlanDetails) => { client.setQueryData(plannerKeys.detail(value.id), value); setSelectedId(value.id); void client.invalidateQueries({ queryKey: plannerKeys.all }); };
  const generate = useMutation({ mutationFn: () => projectPlannerApi.generate(idea), onSuccess: value => { setPlan(value); setIdea(""); show("Draft generated. Review every section before approval."); }, onError: error => show(error.message, "error") });
  const approve = useMutation({ mutationFn: (value: ProjectPlanDetails) => projectPlannerApi.approve(value.id, value.version), onSuccess: value => { setPlan(value); show("Plan approved. Nothing has been created yet."); }, onError: error => show(error.message, "error") });
  const reject = useMutation({ mutationFn: (value: ProjectPlanDetails) => projectPlannerApi.reject(value.id, value.version), onSuccess: value => { setPlan(value); show("Plan rejected."); }, onError: error => show(error.message, "error") });
  const apply = useMutation({ mutationFn: (value: ProjectPlanDetails) => projectPlannerApi.apply(value.id, value.version), onSuccess: projectId => { void client.invalidateQueries({ queryKey: ["projects"] }); show("Approved structure created successfully."); navigate(`/projects/${projectId}/board`); }, onError: error => show(error.message, "error") });
  const detail = selected.data;
  return <main className="planner-page">
    <header className="planner-header"><div><span>OLLAMA · REVIEW BEFORE APPLY</span><h1>AI Project Planner</h1><p>Turn a product idea into architecture, milestones, issues and actionable tasks. Generation never creates resources.</p></div></header>
    <section className="planner-compose"><textarea value={idea} maxLength={2000} onChange={event => setIdea(event.target.value)} placeholder="Describe the product, users, core workflows, constraints and desired outcome…"/><div><small>{idea.length}/2000 · minimum 20 characters</small><button className="ui-button primary" disabled={idea.trim().length < 20 || generate.isPending} onClick={() => generate.mutate()}>{generate.isPending ? "Ollama is planning…" : "Generate review draft"}</button></div></section>
    {generate.isPending && <div className="planner-generating"><i/><div><b>Generating a bounded structured plan</b><span>Ollama is producing seven architecture sections and an implementation breakdown. No project is being created.</span></div></div>}
    <div className="planner-layout"><aside className="planner-history"><h2>Planning drafts</h2>{plans.data?.map(plan => <button key={plan.id} className={selectedId === plan.id ? "active" : ""} onClick={() => setSelectedId(plan.id)}><span>{plan.title}</span><small>{plan.status} · v{plan.version} · {plan.defaultLanguage}</small></button>)}{plans.data?.length === 0 && <p>No plans yet.</p>}</aside>
      <section className="planner-review">{selected.isLoading ? <div className="route-loader">Loading plan…</div> : detail ? <PlanReview plan={detail} busy={approve.isPending || reject.isPending || apply.isPending} approve={() => approve.mutate(detail)} reject={() => reject.mutate(detail)} apply={() => { if (window.confirm(`Create private project “${detail.plan.title}” with all reviewed milestones, issues and tasks?`)) apply.mutate(detail); }}/> : <div className="planner-placeholder"><b>Describe what you want to build</b><p>Your validated Ollama draft will appear here for review.</p></div>}</section>
    </div>
  </main>;
}

function PlanReview({ plan, busy, approve, reject, apply }: { plan: ProjectPlanDetails; busy: boolean; approve: () => void; reject: () => void; apply: () => void }) {
  const blueprint = plan.plan; const issueCount = blueprint.milestones.reduce((n, m) => n + m.issues.length, 0); const taskCount = blueprint.milestones.reduce((n, m) => n + m.issues.reduce((x, i) => x + i.tasks.length, 0), 0);
  return <div className="plan-document"><header><div><span className={`plan-status ${plan.status.toLowerCase()}`}>{plan.status}</span><h2>{blueprint.title}</h2><p>{blueprint.summary}</p></div><dl><div><dt>Language</dt><dd>{blueprint.defaultLanguage}</dd></div><div><dt>Milestones</dt><dd>{blueprint.milestones.length}</dd></div><div><dt>Issues</dt><dd>{issueCount}</dd></div><div><dt>Tasks</dt><dd>{taskCount}</dd></div></dl></header>
    <div className="plan-sections">{Object.entries(blueprint.sections).map(([key, value]) => <article key={key}><small>{sectionLabels[key] ?? key}</small><p>{value}</p></article>)}</div>
    <section className="plan-roadmap"><h3>Delivery roadmap</h3>{blueprint.milestones.map((milestone, mi) => <article className="plan-milestone" key={`${mi}-${milestone.title}`}><header><span>{mi + 1}</span><div><h4>{milestone.title}</h4><p>{milestone.description}</p></div></header>{milestone.issues.map((issue, ii) => <details key={`${ii}-${issue.title}`}><summary><b>{issue.title}</b><span className={`priority ${issue.priority.toLowerCase()}`}>{issue.priority}</span><small>{issue.tasks.length} tasks</small></summary><p>{issue.description}</p><ol>{issue.tasks.map((task, ti) => <li key={`${ti}-${task.title}`}><div><b>{task.title}</b><p>{task.description}</p></div><span>{task.priority}</span></li>)}</ol></details>)}</article>)}</section>
    <footer className="plan-actions"><div><b>{plan.provider}</b><span>{plan.model} · version {plan.version}</span></div>{plan.status === "Draft" && <><button disabled={busy} onClick={reject}>Reject</button><button className="ui-button primary" disabled={busy} onClick={approve}>Approve reviewed plan</button></>}{plan.status === "Approved" && <button className="ui-button primary" disabled={busy} onClick={apply}>{busy ? "Creating…" : "Create project structure"}</button>}{plan.status === "Applied" && plan.createdProjectId && <button onClick={() => window.location.assign(`/projects/${plan.createdProjectId}/board`)}>Open created project</button>}</footer>
  </div>;
}
