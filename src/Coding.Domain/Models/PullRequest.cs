using Coding.Enums;

namespace Coding.Models;

public sealed class PullRequest : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceBranch { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = "main";
    public string SourceHeadSha { get; set; } = string.Empty;
    public string TargetHeadSha { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public PullRequestStatus Status { get; set; } = PullRequestStatus.Open;
    public int RequiredApprovals { get; set; } = 1;
    public bool RequirePassingTests { get; set; }
    public bool? TestsPassed { get; set; }
    public string? TestSummary { get; set; }
    public Guid? MergedById { get; set; }
    public User? MergedBy { get; set; }
    public DateTime? MergedAt { get; set; }
    public string? MergeCommitSha { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<PullRequestReview> Reviews { get; set; } = [];
    public ICollection<PullRequestComment> Comments { get; set; } = [];
}

public sealed class PullRequestReview : Base
{
    public Guid PullRequestId { get; set; }
    public PullRequest PullRequest { get; set; } = null!;
    public Guid ReviewerId { get; set; }
    public User Reviewer { get; set; } = null!;
    public PullRequestReviewDecision Decision { get; set; }
    public string? Body { get; set; }
    public string ReviewedSourceSha { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PullRequestComment : Base
{
    public Guid PullRequestId { get; set; }
    public PullRequest PullRequest { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string? CommitSha { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsResolved { get; set; }
    public Guid? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
