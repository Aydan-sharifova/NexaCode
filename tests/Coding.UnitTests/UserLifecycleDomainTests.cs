using Coding.Enums;
using Coding.Models;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class UserLifecycleDomainTests
{
    [Fact]
    public void New_user_starts_active_with_initial_token_version()
    {
        var user = new User();

        user.Status.Should().Be(UserStatus.Active);
        user.TokenVersion.Should().Be(0);
        user.IsSuspended.Should().BeFalse();
        user.Bans.Should().BeEmpty();
    }

    [Fact]
    public void Timed_ban_distinguishes_expiry_from_permanent_ban()
    {
        var expiry = DateTime.UtcNow.AddDays(7);
        var ban = new UserBan
        {
            Reason = "Workspace policy violation",
            StartAt = DateTime.UtcNow,
            ExpiresAt = expiry,
            IsPermanent = false,
            Status = UserBanStatus.Active
        };

        ban.Status.Should().Be(UserBanStatus.Active);
        ban.IsPermanent.Should().BeFalse();
        ban.ExpiresAt.Should().Be(expiry);
    }

    [Theory]
    [InlineData(UserStatus.Active, 0)]
    [InlineData(UserStatus.Suspended, 1)]
    [InlineData(UserStatus.Banned, 2)]
    [InlineData(UserStatus.Deactivated, 3)]
    [InlineData(UserStatus.Deleted, 4)]
    public void User_status_values_are_stable(UserStatus status, int expected) =>
        ((int)status).Should().Be(expected);
}
