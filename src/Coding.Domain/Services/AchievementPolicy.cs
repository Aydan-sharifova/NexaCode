namespace Coding.Domain.Services;

public static class AchievementPolicy
{
    public const int CommunityMinimumPosts = 3;
    public const int CommunityMinimumComments = 5;
    public const int CommunityMinimumActiveDays = 3;

    public static int CommunityCriteriaMet(int posts, int comments, int activeDays) =>
        (posts >= CommunityMinimumPosts ? 1 : 0) +
        (comments >= CommunityMinimumComments ? 1 : 0) +
        (activeDays >= CommunityMinimumActiveDays ? 1 : 0);

    public static string ContributionLevel(int verifiedAchievementPoints) => verifiedAchievementPoints switch
    {
        >= 500 => "Expert",
        >= 250 => "Contributor",
        >= 100 => "Builder",
        _ => "Newcomer"
    };
}
