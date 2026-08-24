using Coding.Infrastructure.Repositories;
using Coding.Application.Features.Repositories;
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
        await service.StageAsync(projectId, "README.md");
        (await service.GetStatusAsync(projectId)).Files.Should().ContainSingle(item => item.IndexStatus == "M" && item.WorkingTreeStatus == " ");
        (await service.GetDiffAsync(projectId, true)).Patch.Should().Contain("+Changed");
        await service.UnstageAsync(projectId, "README.md");
        (await service.GetStatusAsync(projectId)).Files.Should().ContainSingle(item => item.IndexStatus == " " && item.WorkingTreeStatus == "M");
        (await service.GetHistoryAsync(projectId, 30)).Should().ContainSingle(item => item.Sha == initial.Sha);
        (await service.GetCommitDiffAsync(projectId, initial.Sha)).Patch.Should().Contain("README.md");
    }

    [Fact]
    public async Task Commit_diff_rejects_unsafe_revisions()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");

        var action = () => service.GetCommitDiffAsync(projectId, "HEAD~1;touch-pwned");

        await action.Should().ThrowAsync<ArgumentException>();
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
        await action.Should().ThrowAsync<ArgumentException>();
        File.Exists(marker).Should().BeFalse();
    }

    [Fact]
    public async Task First_branch_can_be_created_in_an_unborn_repository()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");

        await service.CreateBranchAsync(projectId, "feature/first-branch");

        (await service.GetBranchesAsync(projectId)).Should().Contain(item => item.Name == "feature/first-branch");
        (await service.GetHistoryAsync(projectId, 10)).Should().ContainSingle(item => item.Message == "Initialize repository");
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

    [Fact]
    public async Task Stage_paths_cannot_escape_the_project_repository()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var action = () => service.StageAsync(projectId, "../outside.txt");
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Worktree_writes_reject_symbolic_links_and_git_metadata_casing()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        var outside = Path.Combine(_root, "outside.txt");
        await File.WriteAllTextAsync(outside, "unchanged");
        File.CreateSymbolicLink(Path.Combine(repository, "linked.txt"), outside);

        Func<Task> write = () => service.WriteFileAsync(projectId, "linked.txt", "changed"u8.ToArray());
        Func<Task> stage = () => service.StageAsync(projectId, ".GIT/config");
        await write.Should().ThrowAsync<InvalidOperationException>();
        await stage.Should().ThrowAsync<ArgumentException>();
        (await File.ReadAllTextAsync(outside)).Should().Be("unchanged");
    }

    [Fact]
    public async Task Branch_comparison_and_merge_use_real_git_and_restore_the_previous_branch()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "base\n");
        await service.CommitAllAsync(projectId, "Initial", "Test User", "test@example.invalid");
        await service.CreateBranchAsync(projectId, "feature/review");
        await service.CheckoutAsync(projectId, "feature/review");
        await File.AppendAllTextAsync(Path.Combine(repository, "README.md"), "feature\n");
        var feature = await service.CommitAllAsync(projectId, "Feature", "Test User", "test@example.invalid");

        (await service.CompareBranchesAsync(projectId, "main", "feature/review")).Patch.Should().Contain("+feature");
        (await service.GetBranchHeadAsync(projectId, "feature/review")).Should().Be(feature.Sha);
        (await service.HasMergeConflictsAsync(projectId, "main", "feature/review")).Should().BeFalse();

        var merged = await service.MergeAsync(projectId, "main", "feature/review", "Merge feature");

        merged.Sha.Should().HaveLength(40);
        (await service.GetBranchHeadAsync(projectId, "main")).Should().Be(merged.Sha);
        (await service.GetStatusAsync(projectId)).CurrentBranch.Should().Be("feature/review");
    }

    [Fact]
    public async Task Merge_conflict_check_does_not_modify_the_worktree()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(repository, "app.txt"), "base\n");
        await service.CommitAllAsync(projectId, "Initial", "Test User", "test@example.invalid");
        await service.CreateBranchAsync(projectId, "feature/conflict");
        await service.CheckoutAsync(projectId, "feature/conflict");
        await File.WriteAllTextAsync(Path.Combine(repository, "app.txt"), "feature\n");
        await service.CommitAllAsync(projectId, "Feature edit", "Test User", "test@example.invalid");
        await service.CheckoutAsync(projectId, "main");
        await File.WriteAllTextAsync(Path.Combine(repository, "app.txt"), "main\n");
        await service.CommitAllAsync(projectId, "Main edit", "Test User", "test@example.invalid");

        (await service.HasMergeConflictsAsync(projectId, "main", "feature/conflict")).Should().BeTrue();
        (await service.GetStatusAsync(projectId)).CurrentBranch.Should().Be("main");
        (await service.GetStatusAsync(projectId)).IsClean.Should().BeTrue();
    }

    [Fact]
    public async Task Branch_snapshot_reads_nested_text_and_binary_content_without_checkout()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        Directory.CreateDirectory(Path.Combine(repository, "src"));
        await File.WriteAllTextAsync(Path.Combine(repository, "src", "app.cs"), "class Main {}\n");
        await File.WriteAllBytesAsync(Path.Combine(repository, "asset.bin"), [0, 1, 2, 255]);
        await service.CommitAllAsync(projectId, "Initial", "Test User", "test@example.invalid");
        await service.CreateBranchAsync(projectId, "feature/snapshot");
        await service.CheckoutAsync(projectId, "feature/snapshot");
        await File.WriteAllTextAsync(Path.Combine(repository, "src", "app.cs"), "class Feature {}\n");
        await service.CommitAllAsync(projectId, "Feature", "Test User", "test@example.invalid");
        await service.CheckoutAsync(projectId, "main");

        var snapshot = await service.GetBranchFilesAsync(projectId, "feature/snapshot");

        snapshot.Should().Contain(item => item.Path == "src/app.cs" && System.Text.Encoding.UTF8.GetString(item.Content) == "class Feature {}\n");
        snapshot.Should().Contain(item => item.Path == "asset.bin" && item.Content.SequenceEqual(new byte[] { 0, 1, 2, 255 }));
        (await service.GetStatusAsync(projectId)).CurrentBranch.Should().Be("main");
    }

    [Fact]
    public async Task Replacing_worktree_removes_stale_paths_and_preserves_git_metadata()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        Directory.CreateDirectory(Path.Combine(repository, "old"));
        await File.WriteAllTextAsync(Path.Combine(repository, "old", "removed.txt"), "old");
        await service.CommitAllAsync(projectId, "Initial", "Test User", "test@example.invalid");

        await service.ReplaceWorktreeAsync(projectId, [new GitBranchFile("src/new.txt", "new"u8.ToArray())]);

        File.Exists(Path.Combine(repository, "old", "removed.txt")).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(repository, "src", "new.txt"))).Should().Be("new");
        Directory.Exists(Path.Combine(repository, ".git")).Should().BeTrue();
        (await service.GetStatusAsync(projectId)).Files.Should().Contain(item => item.Path == "old/removed.txt");
        (await service.GetStatusAsync(projectId)).Files.Should().Contain(item => item.Path.StartsWith("src", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Merge_waits_for_the_shared_project_repository_lease()
    {
        var projectId = Guid.NewGuid();
        var coordinator = new ProjectRepositoryCoordinator();
        var service = CreateService(coordinator);
        await service.InitializeAsync(projectId, "main");
        var repository = Path.Combine(_root, projectId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(repository, "app.txt"), "main\n");
        await service.CommitAllAsync(projectId, "Initial", "Test User", "test@example.invalid");
        await service.CreateBranchAsync(projectId, "feature/serialized");
        await service.CheckoutAsync(projectId, "feature/serialized");
        await File.WriteAllTextAsync(Path.Combine(repository, "app.txt"), "feature\n");
        await service.CommitAllAsync(projectId, "Feature", "Test User", "test@example.invalid");
        await using var lease = await coordinator.AcquireAsync(projectId);

        var merge = service.MergeAsync(projectId, "main", "feature/serialized", "Merge serialized");
        await Task.Delay(75);
        merge.IsCompleted.Should().BeFalse();

        await lease.DisposeAsync();
        (await merge.WaitAsync(TimeSpan.FromSeconds(5))).Sha.Should().HaveLength(40);
    }

    private NativeGitRepositoryService CreateService(ProjectRepositoryCoordinator? coordinator = null) => new(
        Options.Create(new RepositoryStorageOptions { RootPath = _root }),
        new TestHostEnvironment { ContentRootPath = _root },
        coordinator ?? new ProjectRepositoryCoordinator());

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
