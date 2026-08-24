using System.Data;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.PullRequests;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.PullRequests;

internal static class PullRequestSupport
{
    public static bool CanReview(ProjectRole role) => role is ProjectRole.Owner or ProjectRole.Admin or ProjectRole.Maintainer;

    public static async Task<(PullRequest PullRequest, ProjectRole Role)> RequireAsync(
        AppDbContext db, Guid projectId, int number, Guid userId, CancellationToken ct, bool tracked = true)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, projectId, userId, ct);
        IQueryable<PullRequest> query = db.PullRequests;
        if (!tracked) query = query.AsNoTracking();
        var pullRequest = await query
            .Include(item => item.Author)
            .Include(item => item.Reviews).ThenInclude(item => item.Reviewer)
            .Include(item => item.Comments).ThenInclude(item => item.Author)
            .Include(item => item.Comments).ThenInclude(item => item.ResolvedBy)
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.Number == number, ct)
            ?? throw new NotFoundException("Pull request not found.");
        return (pullRequest, role);
    }

    public static PullRequestUser User(User value) =>
        new(value.ID, value.PublicId, value.UserName, (value.FirstName + " " + value.LastName).Trim(), value.AvatarUrl);

    public static PullRequestListItem ListItem(PullRequest item)
    {
        var approvals = item.Reviews.Count(review => review.Decision == PullRequestReviewDecision.Approved && review.ReviewedSourceSha == item.SourceHeadSha);
        return new(item.ID, item.Number, item.Title, item.SourceBranch, item.TargetBranch, item.SourceHeadSha, item.Status,
            User(item.Author), approvals, item.RequiredApprovals, item.Comments.Count(comment => comment.IsBlocking && !comment.IsResolved),
            item.RequirePassingTests, item.TestsPassed, item.CreatedAt, item.UpdatedAt);
    }

    public static PullRequestCommentItem Comment(PullRequestComment item) =>
        new(item.ID, User(item.Author), item.Body, item.FilePath, item.LineNumber, item.CommitSha, item.IsBlocking,
            item.IsResolved, item.ResolvedBy is null ? null : User(item.ResolvedBy), item.ResolvedAt, item.CreatedAt);

    public static async Task<PullRequestDetails> DetailsAsync(PullRequest item, IGitRepositoryService git, CancellationToken ct)
    {
        var currentReviews = item.Reviews.Where(review => review.ReviewedSourceSha == item.SourceHeadSha).ToList();
        var approvals = currentReviews.Count(review => review.Decision == PullRequestReviewDecision.Approved);
        var reasons = PullRequestMergeRules.Evaluate(item.Status, approvals, item.RequiredApprovals,
            currentReviews.Any(review => review.Decision == PullRequestReviewDecision.ChangesRequested),
            item.Comments.Any(comment => comment.IsBlocking && !comment.IsResolved), item.RequirePassingTests, item.TestsPassed).ToList();
        if (item.Status == PullRequestStatus.Open)
        {
            try
            {
                var sourceHead = await git.GetBranchHeadAsync(item.ProjectId, item.SourceBranch, ct);
                var targetHead = await git.GetBranchHeadAsync(item.ProjectId, item.TargetBranch, ct);
                if (sourceHead != item.SourceHeadSha) reasons.Add("Source branch changed; refresh the pull request and review the new revision.");
                if (targetHead != item.TargetHeadSha) reasons.Add("Target branch changed; refresh the pull request before merging.");
                if (sourceHead == targetHead) reasons.Add("The source branch has no changes to merge.");
                if (await git.HasMergeConflictsAsync(item.ProjectId, item.TargetBranch, item.SourceBranch, ct)) reasons.Add("Branches have merge conflicts.");
            }
            catch (Exception exception) when (exception is InvalidOperationException or DirectoryNotFoundException)
            {
                reasons.Add("Repository branches could not be verified.");
            }
        }
        return new(ListItem(item), item.Description, item.TargetHeadSha, item.MergeCommitSha, item.MergedAt, item.ClosedAt,
            item.Reviews.OrderByDescending(review => review.UpdatedAt).Select(review => new PullRequestReviewItem(review.ID, User(review.Reviewer), review.Decision, review.Body, review.ReviewedSourceSha, review.UpdatedAt)).ToList(),
            item.Comments.OrderBy(comment => comment.CreatedAt).Select(Comment).ToList(), reasons, reasons.Count == 0);
    }

    public static IEnumerable<Guid> NotificationRecipients(PullRequest item, Guid actorId) =>
        item.Reviews.Select(review => review.ReviewerId).Append(item.AuthorId).Where(id => id != actorId).Distinct();
}

public sealed class CreatePullRequestHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, INotificationService notifications, IActivityLogger activity)
    : IRequestHandler<CreatePullRequestCommand, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(CreatePullRequestCommand request, CancellationToken ct)
    {
        await ProjectAccess.RequireWorkspaceWriteAsync(db, request.ProjectId, user.UserId, ct);
        var project = await db.Projects.AsNoTracking().Where(item => item.ID == request.ProjectId)
            .Select(item => new { item.ProtectedBranch, item.RequiredPullRequestApprovals, item.RequirePassingPullRequestTests }).SingleAsync(ct);
        var target = string.IsNullOrWhiteSpace(request.TargetBranch) ? project.ProtectedBranch : request.TargetBranch.Trim();
        var source = request.SourceBranch.Trim();
        if (!string.Equals(target, project.ProtectedBranch, StringComparison.Ordinal))
            throw new ForbiddenException($"Pull requests must target the protected '{project.ProtectedBranch}' branch.");
        if (source == target) throw new ConflictException("Source and target branches must differ.");
        var sourceHead = await git.GetBranchHeadAsync(request.ProjectId, source, ct);
        var targetHead = await git.GetBranchHeadAsync(request.ProjectId, target, ct);
        if (sourceHead == targetHead || string.IsNullOrWhiteSpace((await git.CompareBranchesAsync(request.ProjectId, target, source, ct)).Patch))
            throw new ConflictException("The source branch has no changes to propose.");

        var strategy = db.Database.CreateExecutionStrategy();
        var pullRequest = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.PullRequests.AnyAsync(item => item.ProjectId == request.ProjectId && item.SourceBranch == source && item.Status == PullRequestStatus.Open, ct))
                throw new ConflictException("An open pull request already exists for this source branch.");
            var number = (await db.PullRequests.Where(item => item.ProjectId == request.ProjectId).MaxAsync(item => (int?)item.Number, ct) ?? 0) + 1;
            var now = DateTime.UtcNow;
            var created = new PullRequest
            {
                ID = Guid.NewGuid(), ProjectId = request.ProjectId, Number = number, Title = request.Title.Trim(), Description = request.Description?.Trim(),
                SourceBranch = source, TargetBranch = target, SourceHeadSha = sourceHead, TargetHeadSha = targetHead, AuthorId = user.UserId,
                RequiredApprovals = project.RequiredPullRequestApprovals, RequirePassingTests = project.RequirePassingPullRequestTests,
                CreatedAt = now, UpdatedAt = now, CreatAt = now
            };
            db.PullRequests.Add(created);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return created;
        });
        pullRequest.Author = await db.Users.SingleAsync(item => item.ID == user.UserId, ct);
        await activity.LogAsync(new(user.UserId, request.ProjectId, "PullRequestCreated", nameof(PullRequest), pullRequest.ID, $"Created pull request #{pullRequest.Number}: {pullRequest.Title}."), ct);
        var managers = await db.ProjectMembers.Where(member => member.ProjectId == request.ProjectId && member.UserId != user.UserId &&
            (member.Role == ProjectRole.Owner || member.Role == ProjectRole.Admin || member.Role == ProjectRole.Maintainer)).Select(member => member.UserId).ToListAsync(ct);
        await notifications.CreateManyAsync(managers.Select(id => new CreateNotificationRequest(id, NotificationType.PullRequestCreated, "Pull request ready for review", $"PR #{pullRequest.Number}: {pullRequest.Title}", pullRequest.ProjectId, nameof(PullRequest))), ct);
        return await PullRequestSupport.DetailsAsync(pullRequest, git, ct);
    }
}

public sealed class ListPullRequestsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListPullRequestsQuery, IReadOnlyList<PullRequestListItem>>
{
    public async Task<IReadOnlyList<PullRequestListItem>> Handle(ListPullRequestsQuery request, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, ct);
        var query = db.PullRequests.AsNoTracking().Where(item => item.ProjectId == request.ProjectId);
        if (request.Status.HasValue) query = query.Where(item => item.Status == request.Status);
        var items = await query.Include(item => item.Author).Include(item => item.Reviews).Include(item => item.Comments)
            .OrderByDescending(item => item.UpdatedAt).Take(200).ToListAsync(ct);
        return items.Select(PullRequestSupport.ListItem).ToList();
    }
}

public sealed class GetPullRequestHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetPullRequestQuery, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(GetPullRequestQuery request, CancellationToken ct)
    {
        var (item, _) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct, false);
        return await PullRequestSupport.DetailsAsync(item, git, ct);
    }
}

public sealed class GetPullRequestDiffHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetPullRequestDiffQuery, PullRequestDiff>
{
    public async Task<PullRequestDiff> Handle(GetPullRequestDiffQuery request, CancellationToken ct)
    {
        var (item, _) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct, false);
        var source = await git.GetBranchHeadAsync(item.ProjectId, item.SourceBranch, ct);
        var target = await git.GetBranchHeadAsync(item.ProjectId, item.TargetBranch, ct);
        var diff = await git.CompareBranchesAsync(item.ProjectId, item.TargetBranch, item.SourceBranch, ct);
        return new(source, target, diff.Patch);
    }
}

public sealed class ReviewPullRequestHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, INotificationService notifications, IActivityLogger activity)
    : IRequestHandler<ReviewPullRequestCommand, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(ReviewPullRequestCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, ct);
        if (!PullRequestSupport.CanReview(role)) throw new ForbiddenException("Maintainer access is required to submit a review.");
        if (item.AuthorId == user.UserId) throw new ForbiddenException("Pull request authors cannot approve or request changes on their own pull request.");
        if (item.Status != PullRequestStatus.Open) throw new ConflictException("Only open pull requests can be reviewed.");
        var sourceHead = await git.GetBranchHeadAsync(item.ProjectId, item.SourceBranch, ct);
        if (sourceHead != item.SourceHeadSha) throw new ConflictException("The source branch changed. Refresh the pull request before reviewing it.");
        var now = DateTime.UtcNow;
        var review = item.Reviews.SingleOrDefault(review => review.ReviewerId == user.UserId);
        if (review is null)
        {
            review = new PullRequestReview { ID = Guid.NewGuid(), PullRequestId = item.ID, ReviewerId = user.UserId, CreatedAt = now, CreatAt = now };
            db.PullRequestReviews.Add(review); item.Reviews.Add(review);
        }
        review.Decision = request.Decision; review.Body = request.Body?.Trim(); review.ReviewedSourceSha = sourceHead; review.UpdatedAt = now; review.UpdateAt = now; item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        review.Reviewer = await db.Users.SingleAsync(value => value.ID == user.UserId, ct);
        await activity.LogAsync(new(user.UserId, item.ProjectId, "PullRequestReviewed", nameof(PullRequest), item.ID, $"Submitted {request.Decision} on PR #{item.Number}."), ct);
        await notifications.CreateAsync(new(item.AuthorId, NotificationType.PullRequestReviewed, "Pull request reviewed", $"PR #{item.Number} was marked {request.Decision}.", item.ProjectId, nameof(PullRequest)), ct);
        return await PullRequestSupport.DetailsAsync(item, git, ct);
    }
}

public sealed class AddPullRequestCommentHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, INotificationService notifications)
    : IRequestHandler<AddPullRequestCommentCommand, PullRequestCommentItem>
{
    public async Task<PullRequestCommentItem> Handle(AddPullRequestCommentCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        if (item.Status != PullRequestStatus.Open) throw new ConflictException("Only open pull requests can be commented on.");
        if (request.IsBlocking && !PullRequestSupport.CanReview(role)) throw new ForbiddenException("Only maintainers can create blocking review comments.");
        var sourceHead = await git.GetBranchHeadAsync(item.ProjectId, item.SourceBranch, ct);
        var now = DateTime.UtcNow;
        var comment = new PullRequestComment { ID = Guid.NewGuid(), PullRequestId = item.ID, AuthorId = user.UserId, Body = request.Body.Trim(), FilePath = request.FilePath?.Trim(), LineNumber = request.LineNumber, CommitSha = sourceHead, IsBlocking = request.IsBlocking, CreatedAt = now, CreatAt = now };
        db.PullRequestComments.Add(comment); item.UpdatedAt = now; await db.SaveChangesAsync(ct);
        comment.Author = await db.Users.SingleAsync(value => value.ID == user.UserId, ct);
        await notifications.CreateManyAsync(PullRequestSupport.NotificationRecipients(item, user.UserId).Select(id => new CreateNotificationRequest(id, NotificationType.PullRequestCommented, "Pull request comment", $"A comment was added to PR #{item.Number}.", item.ProjectId, nameof(PullRequest))), ct);
        return PullRequestSupport.Comment(comment);
    }
}

public sealed class ResolvePullRequestCommentHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ResolvePullRequestCommentCommand, PullRequestCommentItem>
{
    public async Task<PullRequestCommentItem> Handle(ResolvePullRequestCommentCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        var comment = item.Comments.SingleOrDefault(value => value.ID == request.CommentId) ?? throw new NotFoundException("Review comment not found.");
        if (comment.IsResolved) return PullRequestSupport.Comment(comment);
        if (comment.AuthorId != user.UserId && item.AuthorId != user.UserId && !PullRequestSupport.CanReview(role)) throw new ForbiddenException("You cannot resolve this review comment.");
        comment.IsResolved = true; comment.ResolvedById = user.UserId; comment.ResolvedAt = DateTime.UtcNow; item.UpdatedAt = comment.ResolvedAt.Value;
        await db.SaveChangesAsync(ct); comment.ResolvedBy = await db.Users.SingleAsync(value => value.ID == user.UserId, ct);
        return PullRequestSupport.Comment(comment);
    }
}

public sealed class RefreshPullRequestHeadHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<RefreshPullRequestHeadCommand, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(RefreshPullRequestHeadCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, ct);
        if (item.Status != PullRequestStatus.Open) throw new ConflictException("Only open pull requests can be refreshed.");
        if (item.AuthorId != user.UserId && !PullRequestSupport.CanReview(role)) throw new ForbiddenException("Only the author or a maintainer can refresh this pull request.");
        var source = await git.GetBranchHeadAsync(item.ProjectId, item.SourceBranch, ct); var target = await git.GetBranchHeadAsync(item.ProjectId, item.TargetBranch, ct);
        if (source == target) throw new ConflictException("The source branch has no changes to merge.");
        if (source != item.SourceHeadSha) { db.PullRequestReviews.RemoveRange(item.Reviews); item.Reviews.Clear(); item.TestsPassed = null; item.TestSummary = null; }
        item.SourceHeadSha = source; item.TargetHeadSha = target; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        return await PullRequestSupport.DetailsAsync(item, git, ct);
    }
}

public sealed class ReportPullRequestTestsHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<ReportPullRequestTestsCommand, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(ReportPullRequestTestsCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        if (!PullRequestSupport.CanReview(role)) throw new ForbiddenException("Maintainer access is required to report test results.");
        if (item.Status != PullRequestStatus.Open) throw new ConflictException("Pull request is not open.");
        if (await git.GetBranchHeadAsync(item.ProjectId, item.SourceBranch, ct) != item.SourceHeadSha) throw new ConflictException("Refresh the changed source revision before reporting tests.");
        item.TestsPassed = request.Passed; item.TestSummary = request.Summary?.Trim(); item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        return await PullRequestSupport.DetailsAsync(item, git, ct);
    }
}

public sealed class MergePullRequestHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, INotificationService notifications, IActivityLogger activity)
    : IRequestHandler<MergePullRequestCommand, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(MergePullRequestCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, ct);
        if (!PullRequestSupport.CanReview(role)) throw new ForbiddenException("Maintainer access is required to merge pull requests.");
        var details = await PullRequestSupport.DetailsAsync(item, git, ct);
        if (!details.CanMerge) throw new ConflictException("Pull request cannot be merged: " + string.Join(' ', details.MergeBlockReasons));
        var result = await git.MergeAsync(item.ProjectId, item.TargetBranch, item.SourceBranch, $"Merge PR #{item.Number}: {item.Title}", ct);
        var now = DateTime.UtcNow; item.Status = PullRequestStatus.Merged; item.MergedById = user.UserId; item.MergedAt = now; item.MergeCommitSha = result.Sha; item.UpdatedAt = now; await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, item.ProjectId, "PullRequestMerged", nameof(PullRequest), item.ID, $"Merged PR #{item.Number}: {item.Title}.", new Dictionary<string, object?> { ["mergeCommitSha"] = result.Sha }), ct);
        await notifications.CreateManyAsync(PullRequestSupport.NotificationRecipients(item, user.UserId).Select(id => new CreateNotificationRequest(id, NotificationType.PullRequestMerged, "Pull request merged", $"PR #{item.Number}: {item.Title}", item.ProjectId, nameof(PullRequest))), ct);
        return await PullRequestSupport.DetailsAsync(item, git, ct);
    }
}

public sealed class ClosePullRequestHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, INotificationService notifications) : IRequestHandler<ClosePullRequestCommand, PullRequestDetails>
{
    public async Task<PullRequestDetails> Handle(ClosePullRequestCommand request, CancellationToken ct)
    {
        var (item, role) = await PullRequestSupport.RequireAsync(db, request.ProjectId, request.Number, user.UserId, ct);
        if (item.Status != PullRequestStatus.Open) throw new ConflictException("Pull request is not open.");
        if (item.AuthorId != user.UserId && !PullRequestSupport.CanReview(role)) throw new ForbiddenException("Only the author or a maintainer can close this pull request.");
        item.Status = PullRequestStatus.Closed; item.ClosedAt = item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        await notifications.CreateManyAsync(PullRequestSupport.NotificationRecipients(item, user.UserId).Select(id => new CreateNotificationRequest(id, NotificationType.PullRequestClosed, "Pull request closed", $"PR #{item.Number}: {item.Title}", item.ProjectId, nameof(PullRequest))), ct);
        return await PullRequestSupport.DetailsAsync(item, git, ct);
    }
}

public sealed class ConfigurePullRequestPolicyHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<ConfigurePullRequestPolicyCommand, PullRequestPolicy>
{
    public async Task<PullRequestPolicy> Handle(ConfigurePullRequestPolicyCommand request, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, ct); ProjectAccess.RequireManager(role);
        await git.GetBranchHeadAsync(request.ProjectId, request.ProtectedBranch.Trim(), ct);
        var project = await db.Projects.SingleAsync(item => item.ID == request.ProjectId, ct);
        project.ProtectedBranch = request.ProtectedBranch.Trim(); project.RequiredPullRequestApprovals = request.RequiredApprovals; project.RequirePassingPullRequestTests = request.RequirePassingTests; project.UpdateAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return new(project.ProtectedBranch, project.RequiredPullRequestApprovals, project.RequirePassingPullRequestTests);
    }
}

public sealed class GetPullRequestPolicyHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetPullRequestPolicyQuery, PullRequestPolicy>
{
    public async Task<PullRequestPolicy> Handle(GetPullRequestPolicyQuery request, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, ct);
        return await db.Projects.AsNoTracking().Where(item => item.ID == request.ProjectId).Select(item => new PullRequestPolicy(item.ProtectedBranch, item.RequiredPullRequestApprovals, item.RequirePassingPullRequestTests)).SingleAsync(ct);
    }
}
