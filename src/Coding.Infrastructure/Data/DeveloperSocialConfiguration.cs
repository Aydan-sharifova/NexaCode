using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class DeveloperProfileConfiguration : IEntityTypeConfiguration<DeveloperProfile>
{
    public void Configure(EntityTypeBuilder<DeveloperProfile> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.User.IsDeleted);
        builder.HasIndex(item => item.UserId).IsUnique();
        builder.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Bio).HasMaxLength(1000);
        builder.Property(item => item.Headline).HasMaxLength(160);
        builder.Property(item => item.CoverImageUrl).HasMaxLength(500);
        builder.Property(item => item.Location).HasMaxLength(120);
        builder.Property(item => item.WebsiteUrl).HasMaxLength(500);
        builder.Property(item => item.GitHubUrl).HasMaxLength(500);
        builder.Property(item => item.LinkedInUrl).HasMaxLength(500);
        builder.Property(item => item.PortfolioUrl).HasMaxLength(500);
        builder.Property(item => item.PrimaryRole).HasMaxLength(100);
        builder.Property(item => item.ExperienceLevel).HasMaxLength(40);
        builder.Property(item => item.Skills).HasColumnType("text[]").HasDefaultValueSql("ARRAY[]::text[]");
        builder.Property(item => item.LearningTopics).HasColumnType("text[]").HasDefaultValueSql("ARRAY[]::text[]");
        builder.HasOne(item => item.User).WithOne(item => item.DeveloperProfile)
            .HasForeignKey<DeveloperProfile>(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Follower.IsDeleted && !item.Following.IsDeleted);
        builder.HasIndex(item => new { item.FollowerId, item.FollowingId }).IsUnique();
        builder.HasIndex(item => new { item.FollowingId, item.CreatedAt });
        builder.ToTable(table => table.HasCheckConstraint("CK_UserFollows_DifferentUsers", "\"FollowerId\" <> \"FollowingId\""));
        builder.HasOne(item => item.Follower).WithMany(item => item.Following)
            .HasForeignKey(item => item.FollowerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Following).WithMany(item => item.Followers)
            .HasForeignKey(item => item.FollowingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Blocker.IsDeleted && !item.Blocked.IsDeleted);
        builder.HasIndex(item => new { item.BlockerId, item.BlockedId }).IsUnique();
        builder.HasIndex(item => new { item.BlockedId, item.CreatedAt });
        builder.ToTable(table => table.HasCheckConstraint("CK_UserBlocks_DifferentUsers", "\"BlockerId\" <> \"BlockedId\""));
        builder.HasOne(item => item.Blocker).WithMany(item => item.BlockedUsers)
            .HasForeignKey(item => item.BlockerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Blocked).WithMany(item => item.BlockedByUsers)
            .HasForeignKey(item => item.BlockedId).OnDelete(DeleteBehavior.Cascade);
    }
}
