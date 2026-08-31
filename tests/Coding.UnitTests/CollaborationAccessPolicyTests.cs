using Coding.Application.Features.Collaboration;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class CollaborationAccessPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ProjectRole.Owner)]
    [InlineData(ProjectRole.Admin)]
    [InlineData(ProjectRole.Maintainer)]
    [InlineData(ProjectRole.Developer)]
    public void Writable_roles_can_publish_realtime_document_updates(ProjectRole role) =>
        CollaborationAccessPolicy.CanWrite(role, ProjectStatus.Active, Now.AddDays(1), Now).Should().BeTrue();

    [Fact]
    public void Viewer_cannot_publish_realtime_document_updates() =>
        CollaborationAccessPolicy.CanWrite(ProjectRole.Viewer, ProjectStatus.Active, null, Now).Should().BeFalse();

    [Fact]
    public void Expired_deadline_blocks_developer_but_not_maintainer() 
    {
        CollaborationAccessPolicy.CanWrite(ProjectRole.Developer, ProjectStatus.Active, Now.AddSeconds(-1), Now).Should().BeFalse();
        CollaborationAccessPolicy.CanWrite(ProjectRole.Maintainer, ProjectStatus.Active, Now.AddSeconds(-1), Now).Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectStatus.Archived)]
    [InlineData(ProjectStatus.Deleted)]
    public void Terminal_project_state_blocks_realtime_writes(ProjectStatus status) =>
        CollaborationAccessPolicy.CanWrite(ProjectRole.Owner, status, null, Now).Should().BeFalse();
}
