import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { Dialog } from "../components/ui/Dialog";
import { useToast } from "../contexts/ToastContext";
import { useProject } from "../features/projects/hooks";
import { usePullRequest, usePullRequestActions, usePullRequestDiff, usePullRequestPolicy, usePullRequests } from "../features/pull-requests/hooks";
import type { PullRequestStatus, ReviewDecision } from "../features/pull-requests/types";
import { repositoryApi } from "../features/repository/api";
import { useAuth } from "../hooks/useAuth";

export function PullRequestsPage() {
  const { projectId = "" } = useParams();
  const navigate = useNavigate();
  const [search, setSearch] = useSearchParams();
  const { session } = useAuth();
  const { show } = useToast();
  const [status, setStatus] = useState<PullRequestStatus | undefined>("Open");
  const selected = Number(search.get("number")) || undefined;
  const project = useProject(projectId);
  const list = usePullRequests(projectId, status);
  const details = usePullRequest(projectId, selected);
  const diff = usePullRequestDiff(projectId, selected);
  const policy = usePullRequestPolicy(projectId);
  const branches = useQuery({ queryKey: ["repository", projectId, "branches"], queryFn: () => repositoryApi.branches(projectId), enabled: Boolean(projectId) });
  const actions = usePullRequestActions(projectId, selected);
  const [createOpen, setCreateOpen] = useState(false);
  const [title, setTitle] = useState(""); const [description, setDescription] = useState(""); const [sourceBranch, setSourceBranch] = useState("");
  const [reviewBody, setReviewBody] = useState("");
  const [commentBody, setCommentBody] = useState(""); const [filePath, setFilePath] = useState(""); const [lineNumber, setLineNumber] = useState(""); const [blocking, setBlocking] = useState(false);
  const [policyOpen, setPolicyOpen] = useState(false); const [requiredApprovals, setRequiredApprovals] = useState(1); const [requireTests, setRequireTests] = useState(false);
  const role = project.data?.currentUserRole;
  const canReview = role === "Owner" || role === "Admin" || role === "Maintainer";
  const canManage = role === "Owner" || role === "Admin";
  const isAuthor = details.data?.pullRequest.author.id === session?.user.id;
  const currentUserReview = useMemo(() => details.data?.reviews.find(item => item.reviewer.id === session?.user.id), [details.data?.reviews, session?.user.id]);

  useEffect(() => {
    if (!selected && list.data?.length) setSearch({ number: String(list.data[0].number) }, { replace: true });
  }, [list.data, selected, setSearch]);
  useEffect(() => {
    if (policy.data) { setRequiredApprovals(policy.data.requiredApprovals); setRequireTests(policy.data.requirePassingTests); }
  }, [policy.data]);

  const run = async (operation: () => Promise<unknown>, success: string) => {
    try { await operation(); show(success); } catch (error) { show(error instanceof Error ? error.message : "Request failed.", "error"); }
  };
  const create = () => run(async () => {
    const result = await actions.create.mutateAsync({ title, description: description || undefined, sourceBranch, targetBranch: policy.data?.protectedBranch });
    setCreateOpen(false); setTitle(""); setDescription(""); setSourceBranch(""); setSearch({ number: String(result.pullRequest.number) });
  }, "Pull request created.");
  const review = (decision: ReviewDecision) => run(async () => { await actions.review.mutateAsync({ decision, body: reviewBody || undefined }); setReviewBody(""); }, decision === "Approved" ? "Pull request approved." : "Changes requested.");
  const comment = () => run(async () => { await actions.comment.mutateAsync({ body: commentBody, filePath: filePath || undefined, lineNumber: lineNumber ? Number(lineNumber) : undefined, isBlocking: blocking }); setCommentBody(""); setFilePath(""); setLineNumber(""); setBlocking(false); }, "Review comment added.");

  if (project.isLoading) return <main className="dashboard-content"><LoadingState label="Loading project…" /></main>;
  if (project.isError || !project.data) return <main className="dashboard-content"><ErrorState message={project.error?.message ?? "Project not found."} /></main>;
  return <main className="dashboard-content feature-page pull-request-page">
    <header className="feature-heading"><div><button className="back-link" onClick={() => navigate(`/projects/${projectId}/workspace`)}>← Workspace</button><h1>Pull requests</h1><p>Protected-branch review and merge workflow for {project.data.name}.</p></div><div className="pr-heading-actions">{canManage && <button className="ui-button ghost" onClick={() => setPolicyOpen(true)}>Merge policy</button>}<button className="ui-button primary" disabled={project.data.isReadOnly} onClick={() => setCreateOpen(true)}>New pull request</button></div></header>
    <div className="pr-layout">
      <aside className="pr-list"><nav>{(["Open", "Merged", "Closed", "All"] as const).map(value => <button key={value} className={(status ?? "All") === value ? "active" : ""} onClick={() => { setStatus(value === "All" ? undefined : value); setSearch({}); }}>{value}</button>)}</nav>{list.isLoading ? <LoadingState /> : list.isError ? <ErrorState message={list.error.message} retry={() => list.refetch()} /> : list.data?.length ? list.data.map(item => <button className={`pr-list-item ${selected === item.number ? "active" : ""}`} key={item.id} onClick={() => setSearch({ number: String(item.number) })}><span>#{item.number} · {item.status}</span><strong>{item.title}</strong><small>{item.sourceBranch} → {item.targetBranch}</small><small>{item.approvalCount}/{item.requiredApprovals} approvals · {item.unresolvedBlockingComments} blockers</small></button>) : <EmptyState title="No pull requests" description="Create a branch with committed changes, then open a pull request." />}</aside>
      <section className="pr-detail">{!selected ? <EmptyState title="Select a pull request" description="Review status, changes, comments, and merge gates." /> : details.isLoading ? <LoadingState /> : details.isError || !details.data ? <ErrorState message={details.error?.message ?? "Pull request not found."} retry={() => details.refetch()} /> : <>
        <header className="pr-detail-header"><div><span className={`status ${details.data.pullRequest.status.toLowerCase()}`}>{details.data.pullRequest.status}</span><h2>#{details.data.pullRequest.number} {details.data.pullRequest.title}</h2><p>{details.data.description || "No description."}</p><small>{details.data.pullRequest.author.fullName} · <code>{details.data.pullRequest.sourceBranch}</code> into <code>{details.data.pullRequest.targetBranch}</code></small></div><div><button className="ui-button ghost" disabled={actions.refresh.isPending || details.data.pullRequest.status !== "Open"} onClick={() => void run(() => actions.refresh.mutateAsync(), "Pull request revision refreshed.")}>Refresh revision</button>{details.data.pullRequest.status === "Open" && (isAuthor || canReview) && <button className="ui-button ghost" onClick={() => void run(() => actions.close.mutateAsync(), "Pull request closed.")}>Close</button>}<button className="ui-button primary" disabled={!canReview || !details.data.canMerge || actions.merge.isPending} onClick={() => void run(() => actions.merge.mutateAsync(), "Pull request merged.")}>Merge</button></div></header>
        <section className={`merge-gates ${details.data.canMerge ? "ready" : "blocked"}`}><h3>{details.data.canMerge ? "Ready to merge" : "Merge protection"}</h3>{details.data.mergeBlockReasons.length ? <ul>{details.data.mergeBlockReasons.map(reason => <li key={reason}>{reason}</li>)}</ul> : <p>Required approvals, tests, comments, revision and conflicts are clear.</p>}</section>
        {canReview && !isAuthor && details.data.pullRequest.status === "Open" && <section className="pr-review-box"><h3>Your review</h3>{currentUserReview && <p>Current decision: <strong>{currentUserReview.decision}</strong></p>}<textarea value={reviewBody} onChange={event => setReviewBody(event.target.value)} placeholder="Review summary (optional)" /><div><button onClick={() => void review("ChangesRequested")}>Request changes</button><button className="ui-button primary" onClick={() => void review("Approved")}>Approve</button></div></section>}
        {details.data.pullRequest.requirePassingTests && canReview && details.data.pullRequest.status === "Open" && <section className="pr-test-box"><h3>Required tests</h3><p>Current result: {details.data.pullRequest.testsPassed === true ? "Passed" : details.data.pullRequest.testsPassed === false ? "Failed" : "Not reported"}</p><div><button onClick={() => void run(() => actions.tests.mutateAsync({ passed: false, summary: "Reported from review UI" }), "Test result recorded.")}>Mark failed</button><button onClick={() => void run(() => actions.tests.mutateAsync({ passed: true, summary: "Reported from review UI" }), "Test result recorded.")}>Mark passed</button></div></section>}
        <section className="pr-diff"><header><h3>Changes</h3><code>{diff.data?.targetHeadSha.slice(0, 7)}…{diff.data?.sourceHeadSha.slice(0, 7)}</code></header>{diff.isLoading ? <LoadingState /> : diff.isError ? <ErrorState message={diff.error.message} retry={() => diff.refetch()} /> : <pre>{diff.data?.patch || "No diff."}</pre>}</section>
        <section className="pr-comments"><h3>Review comments</h3>{details.data.comments.length ? details.data.comments.map(item => <article key={item.id} className={item.isBlocking && !item.isResolved ? "blocking" : ""}><header><strong>{item.author.fullName}</strong><span>{item.isBlocking ? "Blocking" : "Comment"}{item.isResolved ? " · Resolved" : ""}</span></header>{item.filePath && <code>{item.filePath}{item.lineNumber ? `:${item.lineNumber}` : ""}</code>}<p>{item.body}</p>{!item.isResolved && <button onClick={() => void run(() => actions.resolve.mutateAsync(item.id), "Comment resolved.")}>Resolve</button>}</article>) : <p className="muted-copy">No review comments yet.</p>}{details.data.pullRequest.status === "Open" && <div className="pr-comment-form"><textarea value={commentBody} onChange={event => setCommentBody(event.target.value)} placeholder="Add a review comment" /><div><input value={filePath} onChange={event => setFilePath(event.target.value)} placeholder="File path (optional)" /><input type="number" min="1" value={lineNumber} onChange={event => setLineNumber(event.target.value)} placeholder="Line" />{canReview && <label><input type="checkbox" checked={blocking} onChange={event => setBlocking(event.target.checked)} /> Blocking</label>}<button className="ui-button primary" disabled={!commentBody.trim()} onClick={() => void comment()}>Comment</button></div></div>}</section>
      </>}</section>
    </div>
    <Dialog open={createOpen} onClose={() => setCreateOpen(false)} title="Create pull request" description={`Changes will be proposed against protected branch '${policy.data?.protectedBranch ?? "main"}'.`} footer={<><button className="ui-button ghost" onClick={() => setCreateOpen(false)}>Cancel</button><button className="ui-button primary" disabled={!title.trim() || !sourceBranch || actions.create.isPending} onClick={() => void create()}>Create pull request</button></>}><div className="feature-form"><label>Title<input value={title} onChange={event => setTitle(event.target.value)} /></label><label>Description<textarea rows={4} value={description} onChange={event => setDescription(event.target.value)} /></label><label>Source branch<select value={sourceBranch} onChange={event => setSourceBranch(event.target.value)}><option value="">Select a branch</option>{branches.data?.filter(branch => branch.name !== policy.data?.protectedBranch).map(branch => <option key={branch.name}>{branch.name}</option>)}</select></label></div></Dialog>
    <Dialog open={policyOpen} onClose={() => setPolicyOpen(false)} title="Pull request merge policy" description="Policy changes apply to newly created pull requests." footer={<><button className="ui-button ghost" onClick={() => setPolicyOpen(false)}>Cancel</button><button className="ui-button primary" onClick={() => void run(async () => { await actions.policy.mutateAsync({ protectedBranch: policy.data?.protectedBranch ?? "main", requiredApprovals, requirePassingTests: requireTests }); setPolicyOpen(false); }, "Merge policy updated.")}>Save policy</button></>}><div className="feature-form"><label>Protected branch<input value={policy.data?.protectedBranch ?? "main"} disabled /></label><label>Required approvals<input type="number" min="1" max="5" value={requiredApprovals} onChange={event => setRequiredApprovals(Number(event.target.value))} /></label><label className="check-row"><input type="checkbox" checked={requireTests} onChange={event => setRequireTests(event.target.checked)} /><span><strong>Require passing tests</strong><small>A maintainer must report a passing result for the reviewed revision.</small></span></label></div></Dialog>
  </main>;
}
