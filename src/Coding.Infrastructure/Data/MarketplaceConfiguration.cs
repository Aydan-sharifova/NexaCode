using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class MarketplaceItemConfiguration : IEntityTypeConfiguration<MarketplaceItem>
{
    public void Configure(EntityTypeBuilder<MarketplaceItem> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted);
        builder.Property(item => item.Slug).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.TagsJson).HasColumnType("jsonb");
        builder.HasIndex(item => item.Slug).IsUnique();
        builder.HasIndex(item => new { item.Status, item.Category, item.UpdatedAt });
        builder.HasIndex(item => new { item.AuthorId, item.UpdatedAt });
        builder.HasOne(item => item.Author).WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MarketplaceItemVersionConfiguration : IEntityTypeConfiguration<MarketplaceItemVersion>
{
    public void Configure(EntityTypeBuilder<MarketplaceItemVersion> builder)
    {
        builder.HasQueryFilter(version => !version.IsDeleted && !version.MarketplaceItem.IsDeleted);
        builder.Property(version => version.Version).HasMaxLength(40).IsRequired();
        builder.Property(version => version.ManifestJson).HasColumnType("jsonb");
        builder.Property(version => version.PermissionsJson).HasColumnType("jsonb");
        builder.Property(version => version.Checksum).HasMaxLength(64).IsRequired();
        builder.Property(version => version.Changelog).HasMaxLength(4000);
        builder.HasIndex(version => new { version.MarketplaceItemId, version.Version }).IsUnique();
        builder.HasIndex(version => new { version.MarketplaceItemId, version.IsPublished, version.PublishedAt });
        builder.HasOne(version => version.MarketplaceItem).WithMany(item => item.Versions).HasForeignKey(version => version.MarketplaceItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MarketplaceInstallationConfiguration : IEntityTypeConfiguration<MarketplaceInstallation>
{
    public void Configure(EntityTypeBuilder<MarketplaceInstallation> builder)
    {
        builder.HasQueryFilter(installation => !installation.IsDeleted && !installation.Project.IsDeleted && !installation.MarketplaceItem.IsDeleted);
        builder.Property(installation => installation.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(installation => installation.ApprovedPermissionsJson).HasColumnType("jsonb");
        builder.HasIndex(installation => new { installation.ProjectId, installation.MarketplaceItemId }).IsUnique();
        builder.HasIndex(installation => new { installation.ProjectId, installation.Status });
        builder.HasOne(installation => installation.Project).WithMany().HasForeignKey(installation => installation.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(installation => installation.MarketplaceItem).WithMany(item => item.Installations).HasForeignKey(installation => installation.MarketplaceItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(installation => installation.MarketplaceItemVersion).WithMany().HasForeignKey(installation => installation.MarketplaceItemVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(installation => installation.InstalledBy).WithMany().HasForeignKey(installation => installation.InstalledById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MarketplaceLikeConfiguration : IEntityTypeConfiguration<MarketplaceLike>
{
    public void Configure(EntityTypeBuilder<MarketplaceLike> builder)
    {
        builder.HasQueryFilter(item => !item.MarketplaceItem.IsDeleted);
        builder.HasKey(item => new { item.MarketplaceItemId, item.UserId });
        builder.HasOne(item => item.MarketplaceItem).WithMany().HasForeignKey(item => item.MarketplaceItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SavedMarketplaceItemConfiguration : IEntityTypeConfiguration<SavedMarketplaceItem>
{
    public void Configure(EntityTypeBuilder<SavedMarketplaceItem> builder)
    {
        builder.HasQueryFilter(item => !item.MarketplaceItem.IsDeleted);
        builder.HasKey(item => new { item.MarketplaceItemId, item.UserId });
        builder.HasOne(item => item.MarketplaceItem).WithMany().HasForeignKey(item => item.MarketplaceItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
