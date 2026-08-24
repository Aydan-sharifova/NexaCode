using System.Text.Json;
using Coding.Application.Features.Achievements;
using Coding.Application.Features.Notifications;
using Coding.Data;
using Coding.Enums;
using Microsoft.EntityFrameworkCore;
using Coding.Domain.Services;

namespace Coding.Infrastructure.Achievements;

internal sealed record AchievementEvidence(string Code, int Progress, int Target, string EvidenceType, Guid? EvidenceId, DateTime? OccurredAt = null)
{
    public bool Eligible => Progress >= Target;
}

public sealed class AchievementEvaluator(AppDbContext db, INotificationService notifications) : IAchievementEvaluator
{
    public async Task<int> EvaluateAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(x => x.ID == userId && !x.IsDeleted && !x.IsSuspended, ct)) return 0;
        var evidence = await EvidenceAsync(db, userId, ct);
        var catalog = await db.Achievements.Where(x => x.IsActive).ToDictionaryAsync(x => x.Code, ct);
        var awarded = 0;
        foreach (var candidate in evidence.Where(x => x.Eligible && catalog.ContainsKey(x.Code)))
        {
            var achievement = catalog[candidate.Code]; var id = Guid.NewGuid(); var unlocked = candidate.OccurredAt ?? DateTime.UtcNow;
            var json = JsonSerializer.Serialize(new { candidate.Progress, candidate.Target, source = candidate.EvidenceType });
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""UserAchievements"" (""ID"", ""UserId"", ""AchievementId"", ""UnlockedAt"", ""EvidenceType"", ""EvidenceId"", ""EvidenceJson"", ""IsVerified"", ""CreatAt"", ""IsDeleted"")
                VALUES ({id}, {userId}, {achievement.ID}, {unlocked}, {candidate.EvidenceType}, {candidate.EvidenceId}, CAST({json} AS jsonb), TRUE, {DateTime.UtcNow}, FALSE)
                ON CONFLICT (""UserId"", ""AchievementId"") DO NOTHING", ct);
            if (inserted == 0) continue;
            awarded++;
            var postAt=DateTime.UtcNow;
            db.SocialPosts.Add(new Coding.Models.SocialPost { ID=Guid.NewGuid(),AuthorId=userId,Type=PostType.Achievement,
                Content=$"Unlocked verified achievement: {achievement.Title}. {achievement.Description}",CreatedAt=unlocked,UpdatedAt=unlocked,CreatAt=postAt });
            if(candidate.Code==AchievementCodes.FirstDeployment)
            {
                db.SocialPosts.Add(new Coding.Models.SocialPost { ID=Guid.NewGuid(),AuthorId=userId,Type=PostType.Deployment,
                    Content="Completed a verified deployment.",CreatedAt=unlocked,UpdatedAt=unlocked,CreatAt=postAt });
                await notifications.CreateAsync(new(userId,NotificationType.Deployment,"Deployment completed","A verified deployment completed successfully.",candidate.EvidenceId,"Deployment",candidate.EvidenceId.HasValue?$"deployment:{candidate.EvidenceId:N}":$"deployment-achievement:{achievement.ID:N}"),ct);
            }
            await db.SaveChangesAsync(ct);
            await notifications.CreateAsync(new(userId, NotificationType.Achievement, $"Achievement unlocked: {achievement.Title}", achievement.Description, achievement.ID, nameof(Coding.Models.Achievement)), ct);
        }
        return awarded;
    }

    internal static async Task<IReadOnlyList<AchievementEvidence>> EvidenceAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var firstProject = await db.Projects.Where(x => x.OwnerId == userId).OrderBy(x => x.CreatedAt).Select(x => new { x.ID, At = x.CreatedAt }).FirstOrDefaultAsync(ct);
        var firstCommit = await db.GitCommits.Where(x => x.UserId == userId).OrderBy(x => x.CommitDate).Select(x => new { x.ID, At = x.CommitDate }).FirstOrDefaultAsync(ct);
        var firstPr = await db.PullRequests.Where(x => x.AuthorId == userId).OrderBy(x => x.CreatedAt).Select(x => new { x.ID, At = x.CreatedAt }).FirstOrDefaultAsync(ct);
        var firstMerge = await db.PullRequests.Where(x => x.AuthorId == userId && x.Status == PullRequestStatus.Merged).OrderBy(x => x.MergedAt).Select(x => new { x.ID, At = x.MergedAt }).FirstOrDefaultAsync(ct);
        var deployment = await db.ActivityLogs.Where(x => x.UserId == userId && x.ActionType == "DeploymentSucceeded").OrderBy(x => x.CreatedAt).Select(x => new { ID = x.EntityId, At = x.CreatedAt }).FirstOrDefaultAsync(ct);
        var followers = await db.UserFollows.CountAsync(x => x.FollowingId == userId, ct);
        var firstFollower = await db.UserFollows.Where(x => x.FollowingId == userId).OrderBy(x => x.CreatedAt).Select(x => new { x.ID, At = x.CreatedAt }).FirstOrDefaultAsync(ct);
        var posts = await db.SocialPosts.CountAsync(x => x.AuthorId == userId, ct); var comments = await db.SocialPostComments.CountAsync(x => x.AuthorId == userId, ct);
        var activeDays = await db.SocialPosts.Where(x => x.AuthorId == userId).Select(x => x.CreatedAt.Date)
            .Concat(db.SocialPostComments.Where(x => x.AuthorId == userId).Select(x => x.CreatedAt.Date)).Distinct().CountAsync(ct);
        var bugReviews = await db.PullRequestReviews.CountAsync(x => x.ReviewerId == userId && x.Decision == PullRequestReviewDecision.ChangesRequested && x.PullRequest.AuthorId != userId, ct);
        var aiRuns = await db.AiAgentRuns.CountAsync(x => x.UserId == userId && x.Status == AiAgentStatus.Completed, ct);
        var openSource = await db.PullRequests.Where(x => x.AuthorId == userId && x.Status == PullRequestStatus.Merged && x.Project.IsPublic && x.Project.OwnerId != userId).OrderBy(x => x.MergedAt).Select(x => new { x.ID, At = x.MergedAt }).FirstOrDefaultAsync(ct);
        return [
            new(AchievementCodes.FirstProject, firstProject is null ? 0 : 1, 1, nameof(Coding.Models.Project), firstProject?.ID, firstProject?.At),
            new(AchievementCodes.FirstCommit, firstCommit is null ? 0 : 1, 1, nameof(Coding.Models.GitCommit), firstCommit?.ID, firstCommit?.At),
            new(AchievementCodes.FirstPullRequest, firstPr is null ? 0 : 1, 1, nameof(Coding.Models.PullRequest), firstPr?.ID, firstPr?.At),
            new(AchievementCodes.FirstMerge, firstMerge is null ? 0 : 1, 1, nameof(Coding.Models.PullRequest), firstMerge?.ID, firstMerge?.At),
            new(AchievementCodes.FirstDeployment, deployment is null ? 0 : 1, 1, "Deployment", deployment?.ID, deployment?.At),
            new(AchievementCodes.FirstFollower, Math.Min(followers, 1), 1, nameof(Coding.Models.UserFollow), firstFollower?.ID, firstFollower?.At),
            new(AchievementCodes.TenFollowers, Math.Min(followers, 10), 10, nameof(Coding.Models.UserFollow), null),
            new(AchievementCodes.CommunityContributor, AchievementPolicy.CommunityCriteriaMet(posts, comments, activeDays), 3, "CommunityActivity", null),
            new(AchievementCodes.BugHunter, Math.Min(bugReviews, 3), 3, nameof(Coding.Models.PullRequestReview), null),
            new(AchievementCodes.AiBuilder, Math.Min(aiRuns, 3), 3, nameof(Coding.Models.AiAgentRun), null),
            new(AchievementCodes.OpenSourceContributor, openSource is null ? 0 : 1, 1, nameof(Coding.Models.PullRequest), openSource?.ID, openSource?.At)
        ];
    }
}
