import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { pullRequestApi } from "./api";
import type { PullRequestPolicy, PullRequestStatus, ReviewDecision } from "./types";

export const pullRequestKeys = { all: (id: string) => ["pull-requests", id] as const, detail: (id: string, number: number) => ["pull-requests", id, number] as const, diff: (id: string, number: number) => ["pull-requests", id, number, "diff"] as const, policy: (id: string) => ["pull-requests", id, "policy"] as const };
export function usePullRequests(id: string, status?: PullRequestStatus) { return useQuery({ queryKey: [...pullRequestKeys.all(id), status ?? "All"], queryFn: () => pullRequestApi.list(id, status), enabled: Boolean(id) }); }
export function usePullRequest(id: string, number?: number) { return useQuery({ queryKey: pullRequestKeys.detail(id, number ?? 0), queryFn: () => pullRequestApi.get(id, number!), enabled: Boolean(id && number) }); }
export function usePullRequestDiff(id: string, number?: number) { return useQuery({ queryKey: pullRequestKeys.diff(id, number ?? 0), queryFn: () => pullRequestApi.diff(id, number!), enabled: Boolean(id && number) }); }
export function usePullRequestPolicy(id: string) { return useQuery({ queryKey: pullRequestKeys.policy(id), queryFn: () => pullRequestApi.policy(id), enabled: Boolean(id) }); }
export function usePullRequestActions(id: string, number?: number) { const client = useQueryClient(); const refresh = () => { client.invalidateQueries({ queryKey: pullRequestKeys.all(id) }); if (number) { client.invalidateQueries({ queryKey: pullRequestKeys.detail(id, number) }); client.invalidateQueries({ queryKey: pullRequestKeys.diff(id, number) }); } }; return {
  create: useMutation({ mutationFn: (input: { title: string; description?: string; sourceBranch: string; targetBranch?: string }) => pullRequestApi.create(id, input), onSuccess: refresh }),
  review: useMutation({ mutationFn: (input: { decision: ReviewDecision; body?: string }) => pullRequestApi.review(id, number!, input.decision, input.body), onSuccess: refresh }),
  comment: useMutation({ mutationFn: (input: { body: string; filePath?: string; lineNumber?: number; isBlocking: boolean }) => pullRequestApi.comment(id, number!, input), onSuccess: refresh }),
  resolve: useMutation({ mutationFn: (commentId: string) => pullRequestApi.resolve(id, number!, commentId), onSuccess: refresh }),
  refresh: useMutation({ mutationFn: () => pullRequestApi.refresh(id, number!), onSuccess: refresh }),
  merge: useMutation({ mutationFn: () => pullRequestApi.merge(id, number!), onSuccess: refresh }),
  close: useMutation({ mutationFn: () => pullRequestApi.close(id, number!), onSuccess: refresh }),
  tests: useMutation({ mutationFn: (input: { passed: boolean; summary?: string }) => pullRequestApi.tests(id, number!, input.passed, input.summary), onSuccess: refresh }),
  policy: useMutation({ mutationFn: (input: PullRequestPolicy) => pullRequestApi.configurePolicy(id, input), onSuccess: () => client.invalidateQueries({ queryKey: pullRequestKeys.policy(id) }) }),
}; }
