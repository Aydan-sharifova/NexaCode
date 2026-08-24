using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coding.Data;
using Coding.Application.Features.Users;
using Coding.Enums;
using Coding.Infrastructure.Authentication;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Demo;

public sealed class DemoDataSeeder(
    AppDbContext db,
    IdentityPasswordService passwords,
    IPublicUserIdGenerator publicUserIdGenerator,
    ILogger<DemoDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var owner = await EnsureUserAsync(
            DemoDataIds.OwnerUserId,
            "owner@demo.dev",
            "aydan.demo",
            "Aydan",
            "Owner",
            now,
            cancellationToken);
        var admin = await EnsureUserAsync(
            DemoDataIds.AdminUserId,
            "admin@demo.dev",
            "admin.demo",
            "Samir",
            "Admin",
            now,
            cancellationToken);
        var member = await EnsureUserAsync(
            DemoDataIds.MemberUserId,
            "member@demo.dev",
            "developer.demo",
            "Leyla",
            "Developer",
            now,
            cancellationToken);

        if (await db.Projects
                .IgnoreQueryFilters()
                .AnyAsync(project => project.ID == DemoDataIds.ProjectId, cancellationToken))
        {
            logger.LogInformation(
                "Demo seed skipped because project {DemoProjectId} already exists",
                DemoDataIds.ProjectId);
            return;
        }

        var project = new Project
        {
            ID = DemoDataIds.ProjectId,
            Name = "Nebula Commerce Platform",
            Description = "A real-time collaborative e-commerce development workspace.",
            DefaultLanguage = "typescript",
            Owner = owner,
            OwnerId = owner.ID,
            IsPublic = false,
            CreatedAt = now.AddDays(-18),
            CreatAt = now.AddDays(-18)
        };

        project.Members.Add(ProjectMember(project, owner, ProjectRole.Owner, now.AddDays(-18)));
        project.Members.Add(ProjectMember(project, admin, ProjectRole.Admin, now.AddDays(-16)));
        project.Members.Add(ProjectMember(project, member, ProjectRole.Developer, now.AddDays(-12)));

        var src = Folder(project, null, "src", now.AddDays(-18));
        var components = Folder(project, src, "components", now.AddDays(-17));
        var pages = Folder(project, src, "pages", now.AddDays(-16));
        var services = Folder(project, src, "services", now.AddDays(-15));
        var styles = Folder(project, src, "styles", now.AddDays(-14));

        var header = File(
            project,
            components,
            "Header.tsx",
            """
            import { ShoppingBag } from "lucide-react";

            export function Header() {
              return (
                <header className="site-header">
                  <a href="/" className="brand">Nebula</a>
                  <nav aria-label="Primary navigation">
                    <a href="/products">Products</a>
                    <a href="/checkout"><ShoppingBag size={18} /> Cart</a>
                  </nav>
                </header>
              );
            }
            """,
            owner,
            now.AddDays(-17));
        var productCard = File(
            project,
            components,
            "ProductCard.tsx",
            """
            export interface Product {
              id: string;
              name: string;
              price: number;
              imageUrl: string;
            }

            export function ProductCard({ product }: { product: Product }) {
              return (
                <article className="product-card">
                  <img src={product.imageUrl} alt="" />
                  <h2>{product.name}</h2>
                  <p>${product.price.toFixed(2)}</p>
                </article>
              );
            }
            """,
            """
            export interface Product {
              id: string;
              name: string;
              price: number;
              imageUrl: string;
            }

            export function ProductCard({ product }: { product: Product }) {
              return (
                <article className="product-card">
                  <img src={product.imageUrl} alt={`${product.name} product`} loading="lazy" />
                  <div className="product-card__content">
                    <h2>{product.name}</h2>
                    <p aria-label={`Price: $${product.price.toFixed(2)}`}>
                      ${product.price.toFixed(2)}
                    </p>
                  </div>
                </article>
              );
            }
            """,
            member,
            now.AddDays(-9),
            now.AddHours(-3));
        var checkout = File(
            project,
            components,
            "CheckoutForm.tsx",
            """
            import { useState } from "react";

            export function CheckoutForm() {
              const [email, setEmail] = useState("");
              return (
                <form>
                  <label>
                    Email
                    <input value={email} onChange={(event) => setEmail(event.target.value)} />
                  </label>
                  <button type="submit">Place order</button>
                </form>
              );
            }
            """,
            admin,
            now.AddDays(-8));
        var home = File(
            project,
            pages,
            "Home.tsx",
            """
            import { Header } from "../components/Header";

            export default function Home() {
              return (
                <>
                  <Header />
                  <main className="hero">
                    <p className="eyebrow">New collection</p>
                    <h1>Commerce at cosmic speed.</h1>
                  </main>
                </>
              );
            }
            """,
            owner,
            now.AddDays(-13));
        var products = File(
            project,
            pages,
            "Products.tsx",
            """
            import { ProductCard, type Product } from "../components/ProductCard";

            export function Products({ products }: { products: Product[] }) {
              return (
                <main className="product-grid">
                  {products.map((product) => <ProductCard key={product.id} product={product} />)}
                </main>
              );
            }
            """,
            member,
            now.AddDays(-10));
        var productService = File(
            project,
            services,
            "productService.ts",
            """
            import type { Product } from "../components/ProductCard";

            export async function getProducts(signal?: AbortSignal): Promise<Product[]> {
              const response = await fetch("/api/products", { signal });
              if (!response.ok) throw new Error("Products could not be loaded.");
              return response.json() as Promise<Product[]>;
            }
            """,
            admin,
            now.AddDays(-7));
        var globals = File(
            project,
            styles,
            "globals.css",
            """
            :root {
              color: #edf3ff;
              background: #07111f;
              font-family: Inter, system-ui, sans-serif;
            }

            .product-grid {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
              gap: 1.5rem;
            }
            """,
            owner,
            now.AddDays(-14));
        var app = File(
            project,
            src,
            "App.tsx",
            """
            import Home from "./pages/Home";
            import "./styles/globals.css";

            export default function App() {
              return <Home />;
            }
            """,
            owner,
            now.AddDays(-18));

        project.WorkspaceNodes.Add(src);
        project.WorkspaceNodes.Add(components);
        project.WorkspaceNodes.Add(pages);
        project.WorkspaceNodes.Add(services);
        project.WorkspaceNodes.Add(styles);
        project.WorkspaceNodes.Add(header);
        project.WorkspaceNodes.Add(productCard);
        project.WorkspaceNodes.Add(checkout);
        project.WorkspaceNodes.Add(home);
        project.WorkspaceNodes.Add(products);
        project.WorkspaceNodes.Add(productService);
        project.WorkspaceNodes.Add(globals);
        project.WorkspaceNodes.Add(app);

        var improveCheckout = Task(
            project,
            admin,
            "Improve checkout validation",
            "Add client and server validation with accessible inline errors.",
            ProjectTaskStatus.Todo,
            ProjectTaskPriority.High,
            2_000,
            now.AddDays(-4));
        improveCheckout.Assignees.Add(new TaskAssignee
        {
            Task = improveCheckout,
            User = member,
            UserId = member.ID,
            AssignedByUserId = admin.ID,
            AssignedAt = now.AddHours(-6)
        });
        improveCheckout.Comments.Add(new TaskComment
        {
            ID = Guid.NewGuid(),
            Task = improveCheckout,
            User = owner,
            UserId = owner.ID,
            Content = "@Leyla please include keyboard and screen-reader validation states.",
            CreatedAt = now.AddHours(-4),
            CreatAt = now.AddHours(-4)
        });

        var tasks = new[]
        {
            Task(project, owner, "Add product filtering", "Filter by category, price and availability.", ProjectTaskStatus.Todo, ProjectTaskPriority.Medium, 1_000, now.AddDays(-3)),
            improveCheckout,
            Task(project, member, "Build shopping cart", "Persist cart state and calculate totals.", ProjectTaskStatus.Doing, ProjectTaskPriority.High, 1_000, now.AddDays(-6)),
            Task(project, admin, "Add product search", "Debounced search with URL query state.", ProjectTaskStatus.Doing, ProjectTaskPriority.Medium, 2_000, now.AddDays(-5)),
            Task(project, owner, "Create authentication", "Secure account and checkout routes.", ProjectTaskStatus.Done, ProjectTaskPriority.Critical, 1_000, now.AddDays(-15)),
            Task(project, admin, "Configure PostgreSQL", "Add migrations and health checks.", ProjectTaskStatus.Done, ProjectTaskPriority.High, 2_000, now.AddDays(-13)),
            Task(project, member, "Build product cards", "Responsive and accessible product presentation.", ProjectTaskStatus.Done, ProjectTaskPriority.Medium, 3_000, now.AddDays(-9))
        };
        foreach (var task in tasks)
            project.Tasks.Add(task);

        var conversation = new Conversation
        {
            ID = DemoDataIds.ConversationId,
            Type = ConversationType.ProjectChannel,
            Project = project,
            ProjectId = project.ID,
            Name = "Nebula Commerce Platform",
            CreatedAt = now.AddDays(-18),
            UpdatedAt = now.AddMinutes(-18),
            CreatAt = now.AddDays(-18)
        };
        conversation.Participants.Add(Participant(conversation, owner, now.AddDays(-18)));
        conversation.Participants.Add(Participant(conversation, admin, now.AddDays(-16)));
        conversation.Participants.Add(Participant(conversation, member, now.AddDays(-12)));
        conversation.ChatMessages.Add(Message(
            conversation,
            owner,
            "I updated the product card component.",
            now.AddMinutes(-28)));
        conversation.ChatMessages.Add(Message(
            conversation,
            member,
            "I can see the changes in real time.",
            now.AddMinutes(-23)));
        conversation.ChatMessages.Add(Message(
            conversation,
            admin,
            "I assigned the checkout validation task to you.",
            now.AddMinutes(-18)));

        db.Projects.Add(project);
        db.Conversations.Add(conversation);
        db.Notifications.AddRange(
            Notification(member, NotificationType.TaskAssignment, "New task assignment", "You were assigned to “Improve checkout validation”.", improveCheckout.ID, nameof(ProjectTask), now.AddHours(-6), false),
            Notification(member, NotificationType.UserMention, "Aydan mentioned you", "Aydan mentioned you in a project comment.", improveCheckout.ID, nameof(TaskComment), now.AddHours(-4), false),
            Notification(member, NotificationType.Invitation, "Welcome to Nebula", "You were invited to Nebula Commerce Platform.", project.ID, nameof(Project), now.AddDays(-12), true));

        db.ActivityLogs.AddRange(
            Activity(owner, project, "ProjectCreated", nameof(Project), project.ID, "Created Nebula Commerce Platform.", now.AddDays(-18)),
            Activity(admin, project, "MemberRoleChanged", nameof(ProjectMember), admin.ID, "Configured the project administration team.", now.AddDays(-16)),
            Activity(member, project, "FileUpdated", nameof(WorkspaceNode), productCard.ID, "Updated ProductCard.tsx with accessible image and price labels.", now.AddHours(-3)),
            Activity(admin, project, "TaskAssigned", nameof(ProjectTask), improveCheckout.ID, "Assigned checkout validation to Leyla.", now.AddHours(-6)),
            Activity(owner, project, "UserMentioned", nameof(TaskComment), improveCheckout.ID, "Mentioned Leyla in a project comment.", now.AddHours(-4)));

        db.CodingSessions.AddRange(
            Session(owner, project, app, now.AddDays(-6).AddHours(-2), TimeSpan.FromMinutes(84)),
            Session(admin, project, productService, now.AddDays(-4).AddHours(-1), TimeSpan.FromMinutes(57)),
            Session(member, project, productCard, now.AddDays(-2).AddHours(-2), TimeSpan.FromMinutes(112)),
            Session(member, project, checkout, now.AddHours(-5), TimeSpan.FromMinutes(46)));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded demo project {DemoProjectId} with {DemoUserCount} users and {DemoTaskCount} tasks",
            project.ID,
            DemoDataIds.UserIds.Length,
            tasks.Length);
    }

    private async Task<User> EnsureUserAsync(
        Guid id,
        string email,
        string userName,
        string firstName,
        string lastName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.ID == id, cancellationToken);
        if (existing is not null)
            return existing;

        var userRole = await db.Roles.SingleAsync(
            role => role.Name == SystemRoles.User,
            cancellationToken);
        var user = new User
        {
            ID = id,
            Email = email,
            UserName = userName,
            PublicId = await publicUserIdGenerator.GenerateAsync(cancellationToken),
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = string.Empty,
            Bio = "Public demo persona. Changes reset automatically.",
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now,
            LastSeen = now,
            EmailVerifiedAt = now.AddDays(-30),
            RefreshTokens = [],
            AccountTokens = [],
            UserRoles =
            [
                new UserRole
                {
                    ID = Guid.NewGuid(),
                    Role = userRole,
                    RoleId = userRole.ID
                }
            ],
            WorkspaceMembers = [],
            ProjectMembers = [],
            Messages = [],
            Notifications = [],
            CodeHistories = []
        };
        var unexposedPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        user.PasswordHash = passwords.Hash(user, unexposedPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static ProjectMember ProjectMember(
        Project project,
        User user,
        ProjectRole role,
        DateTime joinedAt) =>
        new()
        {
            ID = Guid.NewGuid(),
            Project = project,
            User = user,
            UserId = user.ID,
            Role = role,
            JoinedAt = joinedAt,
            CreatAt = joinedAt
        };

    private static WorkspaceNode Folder(
        Project project,
        WorkspaceNode? parent,
        string name,
        DateTime createdAt) =>
        new()
        {
            ID = Guid.NewGuid(),
            Project = project,
            Parent = parent,
            Name = name,
            NodeType = WorkspaceNodeType.Folder,
            CreatAt = createdAt
        };

    private static WorkspaceNode File(
        Project project,
        WorkspaceNode parent,
        string name,
        string content,
        User author,
        DateTime createdAt) =>
        File(project, parent, name, content, content, author, createdAt, createdAt);

    private static WorkspaceNode File(
        Project project,
        WorkspaceNode parent,
        string name,
        string initialContent,
        string currentContent,
        User author,
        DateTime createdAt,
        DateTime updatedAt)
    {
        var node = new WorkspaceNode
        {
            ID = Guid.NewGuid(),
            Project = project,
            Parent = parent,
            Name = name,
            NodeType = WorkspaceNodeType.File,
            CreatAt = createdAt
        };
        var initialHash = Hash(initialContent);
        var currentHash = Hash(currentContent);
        var hasSecondVersion = !string.Equals(initialContent, currentContent, StringComparison.Ordinal);
        node.FileContent = new FileContent
        {
            Node = node,
            Content = currentContent,
            ContentHash = currentHash,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            VersionNumber = hasSecondVersion ? 2 : 1,
            UpdatedAt = updatedAt,
            UpdatedBy = author,
            UpdatedById = author.ID
        };
        node.Versions.Add(new FileVersion
        {
            ID = Guid.NewGuid(),
            Node = node,
            VersionNumber = 1,
            Content = initialContent,
            ContentHash = initialHash,
            CreatedBy = author,
            CreatedById = author.ID,
            CreatAt = createdAt
        });
        if (hasSecondVersion)
        {
            node.Versions.Add(new FileVersion
            {
                ID = Guid.NewGuid(),
                Node = node,
                VersionNumber = 2,
                Content = currentContent,
                ContentHash = currentHash,
                CreatedBy = author,
                CreatedById = author.ID,
                CreatAt = updatedAt
            });
        }

        return node;
    }

    private static ProjectTask Task(
        Project project,
        User author,
        string title,
        string description,
        ProjectTaskStatus status,
        ProjectTaskPriority priority,
        decimal position,
        DateTime createdAt) =>
        new()
        {
            ID = Guid.NewGuid(),
            Project = project,
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            Position = position,
            CreatedByUser = author,
            CreatedByUserId = author.ID,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            CreatAt = createdAt
        };

    private static ConversationParticipant Participant(
        Conversation conversation,
        User user,
        DateTime joinedAt) =>
        new()
        {
            ID = Guid.NewGuid(),
            Conversation = conversation,
            User = user,
            UserId = user.ID,
            JoinedAt = joinedAt,
            LastReadAt = DateTime.UtcNow
        };

    private static ChatMessage Message(
        Conversation conversation,
        User sender,
        string content,
        DateTime createdAt) =>
        new()
        {
            ID = Guid.NewGuid(),
            Conversation = conversation,
            Sender = sender,
            SenderId = sender.ID,
            Content = content,
            CreatedAt = createdAt,
            CreatAt = createdAt
        };

    private static Notification Notification(
        User user,
        NotificationType type,
        string title,
        string message,
        Guid relatedId,
        string relatedType,
        DateTime createdAt,
        bool isRead) =>
        new()
        {
            ID = Guid.NewGuid(),
            User = user,
            UserId = user.ID,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityId = relatedId,
            RelatedEntityType = relatedType,
            IsRead = isRead,
            ReadAt = isRead ? createdAt.AddMinutes(10) : null,
            CreatedAt = createdAt,
            CreatAt = createdAt
        };

    private static ActivityLog Activity(
        User user,
        Project project,
        string action,
        string entityType,
        Guid entityId,
        string description,
        DateTime createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            User = user,
            UserId = user.ID,
            Project = project,
            ProjectId = project.ID,
            ActionType = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            Metadata = JsonDocument.Parse(
                JsonSerializer.Serialize(new
                {
                    environment = "demo",
                    seed = "nebula-v1"
                })),
            CreatedAt = createdAt
        };

    private static CodingSession Session(
        User user,
        Project project,
        WorkspaceNode file,
        DateTime startAt,
        TimeSpan duration) =>
        new()
        {
            Id = Guid.NewGuid(),
            User = user,
            UserId = user.ID,
            Project = project,
            ProjectId = project.ID,
            File = file,
            FileId = file.ID,
            StartAt = startAt,
            EndAt = startAt.Add(duration),
            LastActivityAt = startAt.Add(duration)
        };

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
