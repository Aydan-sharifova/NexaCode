using System.Security.Cryptography;
using System.Text;
using Coding.Enums;
using Coding.Application.Features.Users;
using Coding.Infrastructure.Authentication;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Coding.Data;

/// <summary>
/// Idempotent sample data for explicitly enabled development environments only.
/// Passwords are required through configuration and are never embedded in source.
/// </summary>
public sealed class DevelopmentDataSeeder(
    AppDbContext db,
    IdentityPasswordService passwords,
    IPublicUserIdGenerator publicUserIdGenerator,
    IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var adminPassword = configuration["DevelopmentSeed:AdminPassword"];
        var demoPassword = configuration["DevelopmentSeed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException(
                "Development seeding requires DevelopmentSeed__AdminPassword and DevelopmentSeed__DemoPassword.");

        var now = DateTime.UtcNow;
        var adminEmail = configuration["DevelopmentSeed:AdminEmail"] ?? "sharifovaydan700@gmail.com";
        var demoEmail = configuration["DevelopmentSeed:DemoEmail"] ?? "demo@coding.local";
        var admin = await EnsureUserAsync(
            adminEmail, "admin", "Development", "Admin", adminPassword,
            [SystemRoles.SuperAdmin, SystemRoles.Admin, SystemRoles.User], now, cancellationToken);
        var demo = await EnsureUserAsync(
            demoEmail, "demo", "Demo", "User", demoPassword,
            [SystemRoles.User], now, cancellationToken);

        var project = await db.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OwnerId == admin.ID && x.Name == "Coding Demo", cancellationToken);
        if (project is not null) return;

        project = new Project
        {
            ID = Guid.NewGuid(),
            Name = "Coding Demo",
            Description = "Sample collaborative project created by the development seeder.",
            DefaultLanguage = "csharp",
            Owner = admin,
            OwnerId = admin.ID,
            CreatedAt = now,
            CreatAt = now,
            Members =
            [
                new ProjectMember { ID = Guid.NewGuid(), User = admin, UserId = admin.ID, Role = ProjectRole.Owner, JoinedAt = now, CreatAt = now },
                new ProjectMember { ID = Guid.NewGuid(), User = demo, UserId = demo.ID, Role = ProjectRole.Developer, JoinedAt = now, CreatAt = now }
            ]
        };

        var src = Folder(project, null, "src", now);
        var docs = Folder(project, null, "docs", now);
        var program = File(project, src, "Program.cs", "Console.WriteLine(\"Hello from Coding!\");\n", admin, now);
        var readme = File(project, docs, "README.md", "# Coding Demo\n\nEdit this file with a teammate and explore version history.\n", admin, now);
        project.WorkspaceNodes.Add(src);
        project.WorkspaceNodes.Add(docs);
        project.WorkspaceNodes.Add(program);
        project.WorkspaceNodes.Add(readme);

        project.Tasks.Add(Task(project, admin, "Review the sample project", ProjectTaskStatus.Todo, ProjectTaskPriority.Medium, 1000, now));
        project.Tasks.Add(Task(project, admin, "Try collaborative editing", ProjectTaskStatus.Doing, ProjectTaskPriority.High, 1000, now));
        project.Tasks.Add(Task(project, admin, "Create the initial workspace", ProjectTaskStatus.Done, ProjectTaskPriority.Low, 1000, now));

        var conversation = new Conversation
        {
            ID = Guid.NewGuid(),
            Type = ConversationType.ProjectChannel,
            Project = project,
            Name = project.Name,
            CreatedAt = now,
            UpdatedAt = now,
            Participants =
            [
                new ConversationParticipant { ID = Guid.NewGuid(), User = admin, UserId = admin.ID, JoinedAt = now },
                new ConversationParticipant { ID = Guid.NewGuid(), User = demo, UserId = demo.ID, JoinedAt = now }
            ]
        };

        db.Projects.Add(project);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> EnsureUserAsync(
        string email,
        string userName,
        string firstName,
        string lastName,
        string password,
        IReadOnlyCollection<string> roleNames,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (existing is not null) return existing;

        var roles = await db.Roles.Where(x => roleNames.Contains(x.Name)).ToListAsync(cancellationToken);
        if (roles.Count != roleNames.Count)
            throw new InvalidOperationException("Required system roles are missing.");

        var user = new User
        {
            ID = Guid.NewGuid(),
            Email = normalizedEmail,
            UserName = userName,
            PublicId = await publicUserIdGenerator.GenerateAsync(cancellationToken),
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            LastSeen = now,
            EmailVerifiedAt = now,
            UserRoles = roles.Select(role => new UserRole { ID = Guid.NewGuid(), Role = role, RoleId = role.ID }).ToList(),
            RefreshTokens = [],
            AccountTokens = [],
            WorkspaceMembers = [],
            ProjectMembers = [],
            Messages = [],
            Notifications = [],
            CodeHistories = []
        };
        user.PasswordHash = passwords.Hash(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static WorkspaceNode Folder(Project project, WorkspaceNode? parent, string name, DateTime now) =>
        new() { ID = Guid.NewGuid(), Project = project, Parent = parent, Name = name, NodeType = WorkspaceNodeType.Folder, CreatAt = now };

    private static WorkspaceNode File(
        Project project, WorkspaceNode parent, string name, string content, User author, DateTime now)
    {
        var node = new WorkspaceNode
        {
            ID = Guid.NewGuid(), Project = project, Parent = parent, Name = name,
            NodeType = WorkspaceNodeType.File, CreatAt = now
        };
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        node.FileContent = new FileContent
        {
            Node = node, Content = content, ContentHash = hash,
            ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 1,
            UpdatedAt = now, UpdatedBy = author, UpdatedById = author.ID
        };
        node.Versions.Add(new FileVersion
        {
            ID = Guid.NewGuid(), Node = node, VersionNumber = 1, Content = content,
            ContentHash = hash, CreatedBy = author, CreatedById = author.ID, CreatAt = now
        });
        return node;
    }

    private static ProjectTask Task(
        Project project, User author, string title, ProjectTaskStatus status,
        ProjectTaskPriority priority, decimal position, DateTime now) =>
        new()
        {
            ID = Guid.NewGuid(), Project = project, Title = title, Status = status,
            Priority = priority, Position = position, CreatedByUser = author,
            CreatedByUserId = author.ID, CreatedAt = now, UpdatedAt = now, CreatAt = now
        };
}
