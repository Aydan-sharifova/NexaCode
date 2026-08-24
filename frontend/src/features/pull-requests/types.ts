export type PullRequestStatus = "Open" | "Merged" | "Closed";
export type ReviewDecision = "Approved" | "ChangesRequested";
export interface PullRequestUser { id: string; publicId: string; userName: string; fullName: string; avatarUrl?: string; }
export interface PullRequestReview { id: string; reviewer: PullRequestUser; decision: ReviewDecision; body?: string; reviewedSourceSha: string; updatedAt: string; }
export interface PullRequestComment { id: string; author: PullRequestUser; body: string; filePath?: string; lineNumber?: number; commitSha?: string; isBlocking: boolean; isResolved: boolean; resolvedBy?: PullRequestUser; resolvedAt?: string; createdAt: string; }
export interface PullRequestListItem { id: string; number: number; title: string; sourceBranch: string; targetBranch: string; sourceHeadSha: string; status: PullRequestStatus; author: PullRequestUser; approvalCount: number; requiredApprovals: number; unresolvedBlockingComments: number; requirePassingTests: boolean; testsPassed?: boolean; createdAt: string; updatedAt: string; }
export interface PullRequestDetails { pullRequest: PullRequestListItem; description?: string; targetHeadSha: string; mergeCommitSha?: string; mergedAt?: string; closedAt?: string; reviews: PullRequestReview[]; comments: PullRequestComment[]; mergeBlockReasons: string[]; canMerge: boolean; }
export interface PullRequestDiff { sourceHeadSha: string; targetHeadSha: string; patch: string; }
export interface PullRequestPolicy { protectedBranch: string; requiredApprovals: number; requirePassingTests: boolean; }
