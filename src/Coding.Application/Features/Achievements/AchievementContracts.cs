using MediatR;

namespace Coding.Application.Features.Achievements;

public sealed record AchievementItem(Guid Id, string Code, string Title, string Description, string Icon, string Category, int Points, bool Unlocked, bool Verified, DateTime? UnlockedAt, string? EvidenceType, Guid? EvidenceId, int Progress, int Target);
public sealed record DeveloperAchievementProfile(Guid UserId, int ReputationScore, string ContributionLevel, int UnlockedCount, int TotalCount, IReadOnlyList<AchievementItem> Achievements);
public sealed record DeveloperJourneyItem(string Code, string Title, string Description, DateTime OccurredAt, Guid? EvidenceId);
public sealed record GetMyAchievementsQuery : IRequest<DeveloperAchievementProfile>;
public sealed record GetUserAchievementsQuery(string PublicId) : IRequest<DeveloperAchievementProfile>;
public sealed record GetDeveloperJourneyQuery(string PublicId) : IRequest<IReadOnlyList<DeveloperJourneyItem>>;

public interface IAchievementEvaluator
{
    Task<int> EvaluateAsync(Guid userId, CancellationToken ct = default);
}

public static class AchievementCodes
{
    public const string FirstProject = "first-project";
    public const string FirstCommit = "first-commit";
    public const string FirstPullRequest = "first-pr";
    public const string FirstMerge = "first-merge";
    public const string FirstDeployment = "first-deployment";
    public const string FirstFollower = "first-follower";
    public const string TenFollowers = "ten-followers";
    public const string CommunityContributor = "community-contributor";
    public const string BugHunter = "bug-hunter";
    public const string AiBuilder = "ai-builder";
    public const string OpenSourceContributor = "open-source-contributor";
}
