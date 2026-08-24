using Coding.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class AchievementPolicyTests
{
    [Theory]
    [InlineData(1000, 0, 1, 1)]
    [InlineData(3, 1000, 1, 2)]
    [InlineData(3, 5, 2, 2)]
    [InlineData(3, 5, 3, 3)]
    public void Community_contribution_requires_varied_sustained_activity(int posts, int comments, int days, int expected) =>
        AchievementPolicy.CommunityCriteriaMet(posts, comments, days).Should().Be(expected);

    [Theory]
    [InlineData(0, "Newcomer")]
    [InlineData(100, "Builder")]
    [InlineData(250, "Contributor")]
    [InlineData(500, "Expert")]
    public void Contribution_level_uses_verified_points(int points, string expected) => AchievementPolicy.ContributionLevel(points).Should().Be(expected);
}
