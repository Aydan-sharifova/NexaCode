using Coding.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Coding.UnitTests;

public sealed class NativeGitRepositoryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "coding-git-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Repository_supports_real_status_commit_branch_history_and_diff()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");

        var repository = Path.Combine(_root, projectId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "# Test\n");
        (await service.GetStatusAsync(projectId)).Files.Should().ContainSingle(item => item.Path == "README.md");

        var initial = await service.CommitAllAsync(projectId, "Initial commit", "Test User", "test@example.invalid");
        initial.Sha.Should().HaveLength(40);
        (await service.GetStatusAsync(projectId)).IsClean.Should().BeTrue();

        await service.CreateBranchAsync(projectId, "feature/editor");
        await service.CheckoutAsync(projectId, "feature/editor");
        (await service.GetBranchesAsync(projectId)).Should().Contain(item => item.Name == "feature/editor" && item.IsCurrent);

        await File.AppendAllTextAsync(Path.Combine(repository, "README.md"), "Changed\n");
        (await service.GetDiffAsync(projectId, false)).Patch.Should().Contain("+Changed");
        (await service.GetHistoryAsync(projectId, 30)).Should().ContainSingle(item => item.Sha == initial.Sha);
    }

    [Fact]
    public async Task Branch_name_is_passed_as_an_argument_and_cannot_execute_shell_commands()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "test");
        await service.CommitAllAsync(projectId, "Initial", "Test User", "test@example.invalid");

        var marker = Path.Combine(_root, "injected");
        var action = () => service.CreateBranchAsync(projectId, $"bad;touch {marker}");
        await action.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(marker).Should().BeFalse();
    }

    [Fact]
    public async Task Worktree_paths_cannot_escape_the_project_repository()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var action = () => service.WriteFileAsync(projectId, "../../outside.txt", "blocked"u8.ToArray());
        await action.Should().ThrowAsync<ArgumentException>();
        File.Exists(Path.Combine(_root, "outside.txt")).Should().BeFalse();
    }

    private NativeGitRepositoryService CreateService() => new(
        Options.Create(new RepositoryStorageOptions { RootPath = _root }),
        new TestHostEnvironment { ContentRootPath = _root });

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Coding.UnitTests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
