using Coding.Domain.Services;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class ProjectLifecycleTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Future_deadline_outside_warning_window_is_active() =>
        ProjectLifecycle.EffectiveStatus(ProjectStatus.Active, Now.AddDays(8), Now)
            .Should().Be(ProjectStatus.Active);

    [Fact]
    public void Deadline_inside_warning_window_is_deadline_soon() =>
        ProjectLifecycle.EffectiveStatus(ProjectStatus.Active, Now.AddDays(3), Now)
            .Should().Be(ProjectStatus.DeadlineSoon);

    [Fact]
    public void Passed_deadline_is_expired() =>
        ProjectLifecycle.EffectiveStatus(ProjectStatus.Active, Now.AddSeconds(-1), Now)
            .Should().Be(ProjectStatus.DeadlineExpired);

    [Theory]
    [InlineData(ProjectStatus.Draft)]
    [InlineData(ProjectStatus.Suspended)]
    [InlineData(ProjectStatus.Archived)]
    [InlineData(ProjectStatus.Deleted)]
    public void Terminal_or_manual_states_are_not_overwritten(ProjectStatus status) =>
        ProjectLifecycle.EffectiveStatus(status, Now.AddDays(-1), Now).Should().Be(status);

    [Fact]
    public void Expiration_only_makes_developer_contributors_read_only()
    {
        ProjectLifecycle.IsWorkspaceReadOnly(ProjectRole.Developer, ProjectStatus.DeadlineExpired).Should().BeTrue();
        ProjectLifecycle.IsWorkspaceReadOnly(ProjectRole.Viewer, ProjectStatus.Active).Should().BeTrue();
        ProjectLifecycle.IsWorkspaceReadOnly(ProjectRole.Maintainer, ProjectStatus.DeadlineExpired).Should().BeFalse();
        ProjectLifecycle.IsWorkspaceReadOnly(ProjectRole.Admin, ProjectStatus.DeadlineExpired).Should().BeFalse();
    }
}
