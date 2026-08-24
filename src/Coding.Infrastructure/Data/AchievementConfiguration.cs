using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    internal static readonly DateTime SeedDate = new(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);
    internal static readonly Achievement[] Catalog =
    [
        Entry("10000000-0000-0000-0000-000000000001", "first-project", "First Project", "Created your first project.", "folder", "Building", 50, 1),
        Entry("10000000-0000-0000-0000-000000000002", "first-commit", "First Commit", "Created your first verified repository commit.", "commit", "Building", 50, 2),
        Entry("10000000-0000-0000-0000-000000000003", "first-pr", "First Pull Request", "Opened your first pull request.", "pull-request", "Collaboration", 60, 3),
        Entry("10000000-0000-0000-0000-000000000004", "first-merge", "First Merge", "Authored a pull request that was merged.", "merge", "Collaboration", 80, 4),
        Entry("10000000-0000-0000-0000-000000000005", "first-deployment", "First Deployment", "Completed a verified deployment.", "rocket", "Delivery", 100, 5),
        Entry("10000000-0000-0000-0000-000000000006", "first-follower", "First Follower", "Earned your first follower.", "user-plus", "Community", 30, 6),
        Entry("10000000-0000-0000-0000-000000000007", "ten-followers", "10 Followers", "Earned ten distinct followers.", "users", "Community", 80, 7),
        Entry("10000000-0000-0000-0000-000000000008", "community-contributor", "Community Contributor", "Contributed posts and helpful comments across multiple days.", "community", "Community", 100, 8),
        Entry("10000000-0000-0000-0000-000000000009", "bug-hunter", "Bug Hunter", "Submitted three changes-requested reviews on other developers' pull requests.", "bug", "Quality", 120, 9),
        Entry("10000000-0000-0000-0000-00000000000a", "ai-builder", "AI Builder", "Completed three bounded AI agent runs.", "sparkles", "AI", 120, 10),
        Entry("10000000-0000-0000-0000-00000000000b", "open-source-contributor", "Open Source Contributor", "Merged a contribution into another owner's public project.", "globe", "Community", 150, 11)
    ];

    public void Configure(EntityTypeBuilder<Achievement> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted);
        b.Property(x => x.Code).HasMaxLength(60);
        b.Property(x => x.Title).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Icon).HasMaxLength(50);
        b.Property(x => x.Category).HasMaxLength(50);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.SortOrder });
        b.HasData(Catalog);
    }

    private static Achievement Entry(string id, string code, string title, string description, string icon, string category, int points, int order) => new() { ID = Guid.Parse(id), Code = code, Title = title, Description = description, Icon = icon, Category = category, Points = points, SortOrder = order, IsActive = true, CreatAt = SeedDate };
}

public sealed class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Achievement.IsDeleted && !x.User.IsDeleted);
        b.Property(x => x.EvidenceType).HasMaxLength(80);
        b.Property(x => x.EvidenceJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.UserId, x.AchievementId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.UnlockedAt });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Achievement).WithMany(x => x.Awards).HasForeignKey(x => x.AchievementId).OnDelete(DeleteBehavior.Restrict);
    }
}
