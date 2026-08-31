using Coding.Application.Features.FileExplorer;
using Coding.Application.Features.Kanban;
using Coding.Application.Features.Projects;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class CoreWorkflowValidationTests
{
    [Fact]
    public async Task Project_creation_requires_a_name_and_language()
    {
        var result = await new CreateProjectValidator().ValidateAsync(
            new CreateProjectCommand("", null, "", false));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(["Name", "DefaultLanguage"]);
    }

    [Fact]
    public async Task Invitation_cannot_grant_owner_role()
    {
        var result = await new InviteProjectMemberValidator().ValidateAsync(
            new InviteProjectMemberCommand(Guid.NewGuid(), "member@example.com", ProjectRole.Owner));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Role");
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("child/folder")]
    [InlineData("bad?.cs")]
    [InlineData("trailing.")]
    public async Task Folder_names_reject_invalid_filesystem_values(string name)
    {
        var result = await new CreateFolderValidator().ValidateAsync(
            new CreateFolderCommand(Guid.NewGuid(), null, name));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task File_save_requires_a_concurrency_token()
    {
        var result = await new SaveFileContentValidator().ValidateAsync(
            new SaveFileContentCommand(Guid.NewGuid(), "content", "short"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "ConcurrencyToken");
    }

    [Fact]
    public async Task Task_move_rejects_identical_neighbors()
    {
        var neighbor = Guid.NewGuid();
        var result = await new MoveTaskValidator().ValidateAsync(
            new MoveTaskCommand(Guid.NewGuid(), ProjectTaskStatus.Doing, neighbor, neighbor));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Task_comment_rejects_empty_content()
    {
        var result = await new AddTaskCommentValidator().ValidateAsync(
            new AddTaskCommentCommand(Guid.NewGuid(), ""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Role_change_cannot_assign_owner()
    {
        var result = await new ChangeProjectMemberRoleValidator().ValidateAsync(
            new ChangeProjectMemberRoleCommand(Guid.NewGuid(), Guid.NewGuid(), ProjectRole.Owner));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Ownership_transfer_requires_project_and_new_owner_ids()
    {
        var result = await new TransferProjectOwnershipValidator().ValidateAsync(
            new TransferProjectOwnershipCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(["ProjectId", "NewOwnerId"]);
    }

    [Fact]
    public async Task Valid_file_and_task_commands_pass_validation()
    {
        var file = await new CreateFileValidator().ValidateAsync(
            new CreateFileCommand(Guid.NewGuid(), null, "Program.cs", ""));
        var task = await new CreateTaskValidator().ValidateAsync(
            new CreateTaskCommand(Guid.NewGuid(), "Add tests", null, ProjectTaskPriority.High, null));

        file.IsValid.Should().BeTrue();
        task.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/absolute")]
    [InlineData(".git")]
    [InlineData("line\nbreak.cs")]
    [InlineData("trailing.")]
    public async Task Workspace_names_reject_path_and_repository_control_values(string name)
    {
        var result = await new CreateFileValidator().ValidateAsync(
            new CreateFileCommand(Guid.NewGuid(), null, name, ""));

        result.IsValid.Should().BeFalse();
    }
}
