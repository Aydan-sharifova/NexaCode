using Coding.Application.Features.Projects;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class ProjectRoleValidationTests
{
    [Fact]
    public void Developer_preserves_the_legacy_member_database_value()
    {
        ((int)ProjectRole.Developer).Should().Be(2);
    }

    [Theory]
    [InlineData(ProjectRole.Admin)]
    [InlineData(ProjectRole.Maintainer)]
    [InlineData(ProjectRole.Developer)]
    [InlineData(ProjectRole.Viewer)]
    public void Invitation_accepts_every_non_owner_project_role(ProjectRole role)
    {
        var validator = new InviteProjectMemberValidator();

        validator.Validate(new InviteProjectMemberCommand(Guid.NewGuid(), "developer@example.com", role))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invitation_rejects_owner_role()
    {
        var validator = new InviteProjectMemberValidator();

        validator.Validate(new InviteProjectMemberCommand(Guid.NewGuid(), "owner@example.com", ProjectRole.Owner))
            .IsValid.Should().BeFalse();
    }
}
