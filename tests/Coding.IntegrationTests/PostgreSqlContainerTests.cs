using Coding.Data;
using Coding.Exceptions;
using Coding.Infrastructure.Users;
using Coding.Infrastructure.Repositories;
using Coding.Application.Features.Repositories;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Infrastructure.Deployments;
using Coding.Enums;
using Coding.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace Coding.IntegrationTests;

public sealed class PostgreSqlContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("coding_tests")
        .WithUsername("coding")
        .WithPassword("coding-tests-only")
        .Build();

    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [DockerFact]
    public async Task PostgreSQL_testcontainer_is_available_for_database_integration_tests()
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1", connection);

        var result = await command.ExecuteScalarAsync();

        result.Should().Be(1);
    }

    [DockerFact]
    public async Task Social_access_denies_interactions_when_either_user_has_blocked_the_other()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        var blocker = NewUser("blocker", now);
        var blocked = NewUser("blocked", now);
        db.Users.AddRange(blocker, blocked);
        db.UserBlocks.Add(new UserBlock
        {
            ID = Guid.NewGuid(),
            BlockerId = blocker.ID,
            BlockedId = blocked.ID,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var service = new SocialAccessService(db);
        (await service.IsBlockedEitherWayAsync(blocker.ID, blocked.ID, default)).Should().BeTrue();
        (await service.IsBlockedEitherWayAsync(blocked.ID, blocker.ID, default)).Should().BeTrue();
        await FluentActions.Invoking(() => service.EnsureCanInteractAsync(blocked.ID, blocker.ID, default))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [DockerFact]
    public async Task Branch_snapshot_import_preserves_common_node_identity_versions_changes_and_soft_deletes_removed_files()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var owner = NewUser("branch_owner", now);
        var project = new Project { ID = Guid.NewGuid(), Name = "Branch import", Owner = owner, OwnerId = owner.ID, DefaultLanguage = "C#", CreatedAt = now, CreatAt = now };
        db.AddRange(owner, project);
        await db.SaveChangesAsync();

        await BranchWorkspaceSynchronizer.ImportAsync(db, project.ID, owner.ID,
            [new GitBranchFile("src/app.cs", "class Main {}"u8.ToArray()), new GitBranchFile("README.md", "old"u8.ToArray())], default);
        db.ChangeTracker.Clear();
        var initial = await db.WorkspaceNodes.Include(node => node.FileContent).SingleAsync(node => node.ProjectId == project.ID && node.Name == "app.cs");
        var initialId = initial.ID;
        var initialToken = initial.FileContent!.ConcurrencyToken;

        await BranchWorkspaceSynchronizer.ImportAsync(db, project.ID, owner.ID,
            [new GitBranchFile("src/app.cs", "class Feature {}"u8.ToArray()), new GitBranchFile("new.txt", "new"u8.ToArray()), new GitBranchFile("assets/icon.bin", [0, 1, 2, 255])], default);
        db.ChangeTracker.Clear();

        var updated = await db.WorkspaceNodes.Include(node => node.FileContent).SingleAsync(node => node.ProjectId == project.ID && node.Name == "app.cs");
        updated.ID.Should().Be(initialId);
        updated.FileContent!.Content.Should().Be("class Feature {}");
        updated.FileContent.VersionNumber.Should().Be(2);
        updated.FileContent.ConcurrencyToken.Should().NotBe(initialToken);
        (await db.WorkspaceNodes.AnyAsync(node => node.ProjectId == project.ID && node.Name == "new.txt")).Should().BeTrue();
        var binary = await db.WorkspaceNodes.Include(node => node.FileContent).SingleAsync(node => node.ProjectId == project.ID && node.Name == "icon.bin");
        binary.FileContent!.IsBinary.Should().BeTrue();
        binary.FileContent.BinaryContent.Should().Equal(0, 1, 2, 255);
        (await db.WorkspaceNodes.IgnoreQueryFilters().SingleAsync(node => node.ProjectId == project.ID && node.Name == "README.md")).IsDeleted.Should().BeTrue();
    }

    [DockerFact]
    public async Task Static_deployment_persists_versioned_assets_and_only_one_active_release()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var owner = NewUser("deploy_owner", now);
        var project = new Project { ID = Guid.NewGuid(), Name = "Static Site", Owner = owner, OwnerId = owner.ID, DefaultLanguage = "HTML", IsPublic = true, CreatedAt = now, CreatAt = now };
        var member = new ProjectMember { ID = Guid.NewGuid(), Project = project, ProjectId = project.ID, User = owner, UserId = owner.ID, Role = ProjectRole.Owner, JoinedAt = now, CreatAt = now };
        var index = new WorkspaceNode { ID = Guid.NewGuid(), Project = project, ProjectId = project.ID, Name = "index.html", NodeType = WorkspaceNodeType.File, CreatAt = now };
        index.FileContent = new FileContent { Node = index, NodeId = index.ID, Content = "<!doctype html><link rel=\"stylesheet\" href=\"site.css\"><h1>Release one</h1>", ContentHash = "a", ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 1, UpdatedAt = now, UpdatedBy = owner, UpdatedById = owner.ID };
        var css = new WorkspaceNode { ID = Guid.NewGuid(), Project = project, ProjectId = project.ID, Name = "site.css", NodeType = WorkspaceNodeType.File, CreatAt = now };
        css.FileContent = new FileContent { Node = css, NodeId = css.ID, Content = "h1{color:rebeccapurple}", ContentHash = "b", ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 1, UpdatedAt = now, UpdatedBy = owner, UpdatedById = owner.ID };
        db.AddRange(owner, project, member, index, css);
        await db.SaveChangesAsync();
        var audit = new CapturingActivityLogger();
        var service = new ProjectDeploymentService(db, new TestCurrentUser(owner.ID, owner.Email), audit);

        var first = await service.DeployAsync(project.ID, default);
        (await service.GetPublicAssetAsync(first.Slug, "site.css", default))!.Content.Should().Contain("rebeccapurple");
        index.FileContent.Content = index.FileContent.Content.Replace("one", "two");
        await db.SaveChangesAsync();
        var second = await service.DeployAsync(project.ID, default);

        second.Version.Should().Be(2);
        second.SourceHash.Should().NotBe(first.SourceHash);
        (await db.ProjectDeployments.CountAsync(x => x.ProjectId == project.ID && x.IsActive)).Should().Be(1);
        audit.Items.Should().HaveCount(2).And.OnlyContain(x => x.ActionType == "DeploymentSucceeded");
    }

    [DockerFact]
    public async Task Public_project_fork_clones_hierarchy_content_and_provenance_as_private()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var owner = NewUser("fork_source", now);
        var developer = NewUser("fork_developer", now);
        var source = new Project { ID = Guid.NewGuid(), Name = new string('x', 120), Owner = owner, OwnerId = owner.ID, DefaultLanguage = "HTML", DatabaseProvider = "PostgreSQL", DatabaseSchemaJson = "{}", IsPublic = true, CreatedAt = now, CreatAt = now };
        var folder = new WorkspaceNode { ID = Guid.NewGuid(), Project = source, ProjectId = source.ID, Name = "src", NodeType = WorkspaceNodeType.Folder, CreatAt = now };
        var file = new WorkspaceNode { ID = Guid.NewGuid(), Project = source, ProjectId = source.ID, Parent = folder, ParentId = folder.ID, Name = "index.html", NodeType = WorkspaceNodeType.File, CreatAt = now };
        file.FileContent = new FileContent { Node = file, NodeId = file.ID, Content = "<h1>Fork me</h1>", ContentHash = "hash", ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 7, UpdatedAt = now, UpdatedBy = owner, UpdatedById = owner.ID };
        db.AddRange(owner, developer, source, folder, file);
        await db.SaveChangesAsync();
        var audit = new CapturingActivityLogger();

        var result = await new ForkPublicProjectHandler(db, new TestCurrentUser(developer.ID, developer.Email), audit)
            .Handle(new(source.ID), default);
        db.ChangeTracker.Clear();

        var fork = await db.Projects.Include(x => x.Members).SingleAsync(x => x.ID == result.ProjectId);
        fork.IsPublic.Should().BeFalse();
        fork.ForkedFromProjectId.Should().Be(source.ID);
        fork.Name.Should().HaveLength(120).And.EndWith(" Fork");
        fork.Members.Should().ContainSingle(x => x.UserId == developer.ID && x.Role == ProjectRole.Owner);
        var clonedFile = await db.WorkspaceNodes.Include(x => x.Parent).Include(x => x.FileContent).SingleAsync(x => x.ProjectId == fork.ID && x.Name == "index.html");
        clonedFile.Parent!.Name.Should().Be("src");
        clonedFile.FileContent!.Content.Should().Be("<h1>Fork me</h1>");
        clonedFile.FileContent.VersionNumber.Should().Be(1);
        (await db.FileVersions.CountAsync(x => x.NodeId == clonedFile.ID)).Should().Be(1);
        audit.Items.Should().ContainSingle(x => x.ActionType == "ProjectForked");
    }

    private static User NewUser(string name, DateTime now) => new()
    {
        ID = Guid.NewGuid(),
        PublicId = $"usr_{Guid.NewGuid():N}",
        UserName = $"{name}_{Guid.NewGuid():N}",
        Email = $"{name}_{Guid.NewGuid():N}@tests.local",
        FirstName = name,
        LastName = "integration",
        PasswordHash = "integration-test-only",
        CreatedAt = now,
        UpdatedAt = now,
        LastSeen = now
    };
}

file sealed record TestCurrentUser(Guid UserId, string Email) : ICurrentUser;
file sealed class CapturingActivityLogger : IActivityLogger
{
    public List<ActivityWrite> Items { get; } = [];
    public Task LogAsync(ActivityWrite activity, CancellationToken cancellationToken = default) { Items.Add(activity); return Task.CompletedTask; }
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerIsRunning())
            Skip = "Docker is not running; start Docker Desktop to execute PostgreSQL integration tests.";
    }

    private static bool DockerIsRunning()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process is not null &&
                   process.WaitForExit(3000) &&
                   process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
