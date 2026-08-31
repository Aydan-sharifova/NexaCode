using Coding.Data;
using Coding.Exceptions;
using Coding.Infrastructure.Users;
using Coding.Infrastructure.Repositories;
using Coding.Application.Features.Repositories;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.Projects;
using Coding.Infrastructure.Deployments;
using Coding.Infrastructure.Projects;
using Coding.Infrastructure.Search;
using Coding.Application.Features.Search;
using Coding.Application.Features.DatabaseMetadata;
using Coding.Infrastructure.DatabaseMetadata;
using Coding.Infrastructure.Authentication;
using Coding.Application.Features.Demo;
using Coding.DTOS.Auth;
using Coding.Services.Interfaces;
using Coding.Enums;
using Coding.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

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

    [DockerFact]
    public async Task Project_owner_can_atomically_transfer_ownership_to_an_existing_member()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var owner = NewUser("transfer_owner", now);
        var successor = NewUser("transfer_successor", now);
        var project = new Project { ID = Guid.NewGuid(), Name = "Transfer", Owner = owner, OwnerId = owner.ID, DefaultLanguage = "C#", CreatedAt = now, CreatAt = now };
        db.AddRange(owner, successor, project,
            new ProjectMember { ID = Guid.NewGuid(), Project = project, User = owner, Role = ProjectRole.Owner, JoinedAt = now, CreatAt = now },
            new ProjectMember { ID = Guid.NewGuid(), Project = project, User = successor, Role = ProjectRole.Developer, JoinedAt = now, CreatAt = now });
        await db.SaveChangesAsync();
        var notifications = new CapturingNotificationService();

        await new TransferProjectOwnershipHandler(db, new TestCurrentUser(owner.ID, owner.Email), notifications)
            .Handle(new TransferProjectOwnershipCommand(project.ID, successor.ID), default);
        db.ChangeTracker.Clear();

        (await db.Projects.SingleAsync(item => item.ID == project.ID)).OwnerId.Should().Be(successor.ID);
        var roles = await db.ProjectMembers.Where(item => item.ProjectId == project.ID).ToDictionaryAsync(item => item.UserId, item => item.Role);
        roles[owner.ID].Should().Be(ProjectRole.Admin);
        roles[successor.ID].Should().Be(ProjectRole.Owner);
        notifications.Items.Should().ContainSingle(item => item.UserId == successor.ID && item.Type == NotificationType.RoleChange);
    }

    [DockerFact]
    public async Task User_search_hides_private_and_blocked_profiles_and_supports_at_public_id()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var viewer = NewUser("search_viewer", now);
        var visible = NewUser("search_visible", now);
        var hidden = NewUser("search_hidden", now);
        var blocked = NewUser("search_blocked", now);
        var blockedProject = new Project { ID = Guid.NewGuid(), Name = "Search leak project", Owner = blocked, OwnerId = blocked.ID, DefaultLanguage = "C#", IsPublic = true, CreatedAt = now, CreatAt = now };
        hidden.DeveloperProfile = new DeveloperProfile { ID = Guid.NewGuid(), User = hidden, UserId = hidden.ID, DisplayName = "Hidden", IsProfilePublic = false, CreatedAt = now, UpdatedAt = now, CreatAt = now };
        db.AddRange(viewer, visible, hidden, blocked, blockedProject,
            new UserBlock { ID = Guid.NewGuid(), Blocker = viewer, BlockerId = viewer.ID, Blocked = blocked, BlockedId = blocked.ID, CreatedAt = now, CreatAt = now });
        await db.SaveChangesAsync();
        var lookup = new UserLookupService(db);

        var results = await lookup.SearchAsync(viewer.ID, "search", 1, 20, default);
        results.Items.Should().Contain(item => item.PublicId == visible.PublicId);
        results.Items.Should().NotContain(item => item.PublicId == hidden.PublicId || item.PublicId == blocked.PublicId);
        var byPublicId = await lookup.SearchAsync(viewer.ID, $"@{visible.PublicId}", 1, 20, default);
        byPublicId.Items.Should().ContainSingle(item => item.PublicId == visible.PublicId);
        var byExactEmail = await lookup.SearchAsync(viewer.ID, visible.Email, 1, 20, default);
        byExactEmail.Items.Should().ContainSingle(item => item.PublicId == visible.PublicId);
        var global = new GlobalSearchHandler(db, new TestCurrentUser(viewer.ID, viewer.Email));
        var globalUsers = await global.Handle(new GlobalSearchQuery("search", SearchResultType.User, PageSize: 20), default);
        globalUsers.Groups.Single().Items.Should().Contain(item => item.NavigationUrl.EndsWith(visible.PublicId));
        globalUsers.Groups.Single().Items.Should().NotContain(item => item.NavigationUrl.EndsWith(hidden.PublicId) || item.NavigationUrl.EndsWith(blocked.PublicId));
        var globalProjects = await global.Handle(new GlobalSearchQuery("leak", SearchResultType.Project, PageSize: 20), default);
        globalProjects.Groups.Single().Items.Should().BeEmpty();
    }

    [DockerFact]
    public async Task Database_migration_requires_preview_version_and_applies_atomically()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var owner = NewUser("database_owner", now);
        var initial = new List<DatabaseSchemaDto> { new("public", []) };
        var project = new Project { ID = Guid.NewGuid(), Name = "Database", Owner = owner, OwnerId = owner.ID, DefaultLanguage = "C#", DatabaseProvider = "PostgreSQL", DatabaseSchemaJson = System.Text.Json.JsonSerializer.Serialize(initial), DatabaseSchemaVersion = 1, CreatedAt = now, CreatAt = now };
        db.AddRange(owner, project, new ProjectMember { ID = Guid.NewGuid(), Project = project, User = owner, Role = ProjectRole.Owner, JoinedAt = now, CreatAt = now });
        await db.SaveChangesAsync();
        var current = new TestCurrentUser(owner.ID, owner.Email);
        var draft = await new CreateTableMigrationHandler(db, current).Handle(new(project.ID, "add_projects", "public", "projects", [new("id", "uuid", false, true), new("name", "string", false, false)], 1), default);

        draft.Status.Should().Be("Draft");
        draft.DdlPreview.Should().Contain("CREATE TABLE \"public\".\"projects\"");
        var applied = await new ApplyDatabaseMigrationHandler(db, current).Handle(new(project.ID, draft.Id, 1, true), default);

        applied.Version.Should().Be(2);
        applied.Schemas.Single().Tables.Should().ContainSingle(x => x.Name == "projects");
        (await db.ProjectDatabaseMigrations.SingleAsync(x => x.ID == draft.Id)).Status.Should().Be(ProjectDatabaseMigrationStatus.Applied);
        await FluentActions.Invoking(() => new ApplyDatabaseMigrationHandler(db, current).Handle(new(project.ID, draft.Id, 1, true), default)).Should().ThrowAsync<ConflictException>();
    }

    [DockerFact]
    public async Task Reused_rotated_refresh_token_revokes_its_entire_session_family()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var role = new Role { ID = Guid.NewGuid(), Name = "User", CreatAt = now };
        var user = NewUser("refresh_family", now); user.EmailVerifiedAt = now;
        var passwords = new IdentityPasswordService(); user.PasswordHash = passwords.Hash(user, "ValidPass123!");
        db.AddRange(role, user, new UserRole { User = user, Role = role });
        await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = new string('k', 64), ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests", ["Jwt:AccessTokenMinutes"] = "15" }).Build();
        var service = new AuthenticationService(db, Mock.Of<IEmailSender>(), configuration, passwords, new CapturingActivityLogger(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new DisabledDemoEnvironmentService(), new PublicUserIdGenerator(db));
        var login = await service.LoginAsync(new LoginRequest { Email = user.Email, Password = "ValidPass123!" }, default);
        var rotated = await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }, default);

        await FluentActions.Invoking(() => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }, default)).Should().ThrowAsync<UnauthorizedException>();
        await FluentActions.Invoking(() => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = rotated.RefreshToken }, default)).Should().ThrowAsync<UnauthorizedException>();
        (await db.RefreshTokens.Where(x => x.UserId == user.ID).ToListAsync()).Should().OnlyContain(x => x.IsRevoked);
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

file sealed class CapturingNotificationService : INotificationService
{
    public List<CreateNotificationRequest> Items { get; } = [];
    public Task<NotificationItem?> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default) { Items.Add(request); return Task.FromResult<NotificationItem?>(null); }
    public Task<IReadOnlyList<NotificationItem>> CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken cancellationToken = default) { Items.AddRange(requests); return Task.FromResult<IReadOnlyList<NotificationItem>>([]); }
    public Task MarkRelatedReadAsync(Guid userId, NotificationType type, Guid relatedEntityId, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
