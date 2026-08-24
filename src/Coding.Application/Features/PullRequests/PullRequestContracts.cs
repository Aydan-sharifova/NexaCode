using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.PullRequests;

public sealed record PullRequestUser(Guid Id, string PublicId, string UserName, string FullName, string? AvatarUrl);
public sealed record PullRequestReviewItem(Guid Id, PullRequestUser Reviewer, PullRequestReviewDecision Decision, string? Body, string ReviewedSourceSha, DateTime UpdatedAt);
public sealed record PullRequestCommentItem(Guid Id, PullRequestUser Author, string Body, string? FilePath, int? LineNumber, string? CommitSha, bool IsBlocking, bool IsResolved, PullRequestUser? ResolvedBy, DateTime? ResolvedAt, DateTime CreatedAt);
public sealed record PullRequestListItem(Guid Id, int Number, string Title, string SourceBranch, string TargetBranch, string SourceHeadSha, PullRequestStatus Status, PullRequestUser Author, int ApprovalCount, int RequiredApprovals, int UnresolvedBlockingComments, bool RequirePassingTests, bool? TestsPassed, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record PullRequestDetails(PullRequestListItem PullRequest, string? Description, string TargetHeadSha, string? MergeCommitSha, DateTime? MergedAt, DateTime? ClosedAt, IReadOnlyList<PullRequestReviewItem> Reviews, IReadOnlyList<PullRequestCommentItem> Comments, IReadOnlyList<string> MergeBlockReasons, bool CanMerge);
public sealed record PullRequestDiff(string SourceHeadSha, string TargetHeadSha, string Patch);
public sealed record PullRequestPolicy(string ProtectedBranch, int RequiredApprovals, bool RequirePassingTests);

public static class PullRequestMergeRules
{
    public static IReadOnlyList<string> Evaluate(PullRequestStatus status, int approvals, int requiredApprovals,
        bool hasChangesRequested, bool hasBlockingComments, bool requirePassingTests, bool? testsPassed)
    {
        var reasons = new List<string>();
        if (status != PullRequestStatus.Open) reasons.Add("Pull request is not open.");
        if (approvals < requiredApprovals) reasons.Add($"{requiredApprovals - approvals} required approval(s) are missing.");
        if (hasChangesRequested) reasons.Add("A reviewer requested changes.");
        if (hasBlockingComments) reasons.Add("Blocking review comments remain unresolved.");
        if (requirePassingTests && testsPassed != true) reasons.Add("Required tests have not passed for this revision.");
        return reasons;
    }
}

public sealed record CreatePullRequestCommand(Guid ProjectId, string Title, string? Description, string SourceBranch, string? TargetBranch) : IRequest<PullRequestDetails>;
public sealed record ListPullRequestsQuery(Guid ProjectId, PullRequestStatus? Status = null) : IRequest<IReadOnlyList<PullRequestListItem>>;
public sealed record GetPullRequestQuery(Guid ProjectId, int Number) : IRequest<PullRequestDetails>;
public sealed record GetPullRequestDiffQuery(Guid ProjectId, int Number) : IRequest<PullRequestDiff>;
public sealed record ReviewPullRequestCommand(Guid ProjectId, int Number, PullRequestReviewDecision Decision, string? Body) : IRequest<PullRequestDetails>;
public sealed record AddPullRequestCommentCommand(Guid ProjectId, int Number, string Body, string? FilePath, int? LineNumber, bool IsBlocking) : IRequest<PullRequestCommentItem>;
public sealed record ResolvePullRequestCommentCommand(Guid ProjectId, int Number, Guid CommentId) : IRequest<PullRequestCommentItem>;
public sealed record RefreshPullRequestHeadCommand(Guid ProjectId, int Number) : IRequest<PullRequestDetails>;
public sealed record MergePullRequestCommand(Guid ProjectId, int Number) : IRequest<PullRequestDetails>;
public sealed record ClosePullRequestCommand(Guid ProjectId, int Number) : IRequest<PullRequestDetails>;
public sealed record ReportPullRequestTestsCommand(Guid ProjectId, int Number, bool Passed, string? Summary) : IRequest<PullRequestDetails>;
public sealed record ConfigurePullRequestPolicyCommand(Guid ProjectId, string ProtectedBranch, int RequiredApprovals, bool RequirePassingTests) : IRequest<PullRequestPolicy>;
public sealed record GetPullRequestPolicyQuery(Guid ProjectId) : IRequest<PullRequestPolicy>;

public sealed class CreatePullRequestValidator : AbstractValidator<CreatePullRequestCommand>
{
    public CreatePullRequestValidator()
    {
        RuleFor(item => item.ProjectId).NotEmpty();
        RuleFor(item => item.Title).NotEmpty().MaximumLength(180);
        RuleFor(item => item.Description).MaximumLength(5000);
        RuleFor(item => item.SourceBranch).NotEmpty().MaximumLength(200);
        RuleFor(item => item.TargetBranch).MaximumLength(200);
    }
}

public sealed class ReviewPullRequestValidator : AbstractValidator<ReviewPullRequestCommand>
{
    public ReviewPullRequestValidator() => RuleFor(item => item.Body).MaximumLength(3000);
}

public sealed class AddPullRequestCommentValidator : AbstractValidator<AddPullRequestCommentCommand>
{
    public AddPullRequestCommentValidator()
    {
        RuleFor(item => item.Body).NotEmpty().MaximumLength(5000);
        RuleFor(item => item.FilePath).MaximumLength(500);
        RuleFor(item => item.LineNumber).GreaterThan(0).When(item => item.LineNumber.HasValue);
        RuleFor(item => item).Must(item => item.FilePath is not null || item.LineNumber is null)
            .WithMessage("A line number requires a file path.");
    }
}

public sealed class ConfigurePullRequestPolicyValidator : AbstractValidator<ConfigurePullRequestPolicyCommand>
{
    public ConfigurePullRequestPolicyValidator()
    {
        RuleFor(item => item.ProtectedBranch).NotEmpty().MaximumLength(200);
        RuleFor(item => item.RequiredApprovals).InclusiveBetween(1, 5);
    }
}
