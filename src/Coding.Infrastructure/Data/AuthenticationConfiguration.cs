using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(item => item.Email).IsUnique();
        builder.HasIndex(item => item.UserName).IsUnique();
        builder.HasIndex(item => item.PublicId).IsUnique();
        builder.Property(item => item.Email).HasMaxLength(254);
        builder.Property(item => item.UserName).HasMaxLength(50);
        builder.Property(item => item.PublicId).HasMaxLength(8).IsRequired();
        builder.Property(item => item.PasswordHash).HasMaxLength(100);
        builder.Property(item => item.SuspensionReason).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Coding.Enums.UserStatus.Active);
        builder.Property(item => item.SubscriptionPlan).HasMaxLength(20).HasDefaultValue("Free");
        builder.Property(item => item.SubscriptionStatus).HasMaxLength(30).HasDefaultValue("inactive");
        builder.Property(item => item.StripeCustomerId).HasMaxLength(100);
        builder.Property(item => item.StripeSubscriptionId).HasMaxLength(100);
        builder.HasIndex(item => item.StripeCustomerId).IsUnique();
        builder.HasIndex(item => item.StripeSubscriptionId).IsUnique();
    }
}

public sealed class UserBanConfiguration : IEntityTypeConfiguration<UserBan>
{
    public void Configure(EntityTypeBuilder<UserBan> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(item => new { item.UserId, item.Status, item.ExpiresAt });
        builder.HasOne(item => item.User).WithMany(item => item.Bans).HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BannedByUser).WithMany().HasForeignKey(item => item.BannedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasIndex(item => item.Name).IsUnique();
        builder.Property(item => item.Name).HasMaxLength(50);
        builder.HasData(
            new Role
            {
                ID = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Admin",
                Description = "Built-in Admin role.",
                CreatAt = DateTime.UnixEpoch
            },
            new Role
            {
                ID = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Developer",
                Description = "Built-in Developer role.",
                CreatAt = DateTime.UnixEpoch
            },
            new Role
            {
                ID = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Guest",
                Description = "Built-in Guest role.",
                CreatAt = DateTime.UnixEpoch
            },
            new Role
            {
                ID = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "SuperAdmin",
                Description = "Built-in platform owner role.",
                CreatAt = DateTime.UnixEpoch
            },
            new Role
            {
                ID = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "User",
                Description = "Built-in platform user role.",
                CreatAt = DateTime.UnixEpoch
            },
            new Role
            {
                ID = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "Moderator",
                Description = "Built-in moderation role.",
                CreatAt = DateTime.UnixEpoch
            });
    }
}

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.Theme).HasMaxLength(20);
        builder.Property(x => x.Language).HasMaxLength(10);
        builder.HasOne(x => x.User).WithOne().HasForeignKey<UserPreference>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.HasIndex(x => x.RefreshTokenId).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RefreshToken).WithOne().HasForeignKey<UserSession>(x => x.RefreshTokenId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasIndex(item => new { item.UserId, item.RoleId }).IsUnique();
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(item => item.Token).IsUnique();
        builder.Property(item => item.Token).HasMaxLength(64);
        builder.HasOne(item => item.User)
            .WithMany(item => item.RefreshTokens)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AccountTokenConfiguration : IEntityTypeConfiguration<AccountToken>
{
    public void Configure(EntityTypeBuilder<AccountToken> builder)
    {
        builder.HasIndex(item => item.TokenHash).IsUnique();
        builder.Property(item => item.TokenHash).HasMaxLength(64);
        builder.HasOne(item => item.User)
            .WithMany(item => item.AccountTokens)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
