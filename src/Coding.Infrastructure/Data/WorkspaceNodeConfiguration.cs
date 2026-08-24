using Coding.Enums;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class WorkspaceNodeConfiguration : IEntityTypeConfiguration<WorkspaceNode>
{
    public void Configure(EntityTypeBuilder<WorkspaceNode> builder)
    {
        builder.HasQueryFilter(node => !node.IsDeleted && !node.Project.IsDeleted);
        builder.Property(node => node.Name).HasColumnType("citext").HasMaxLength(255).IsRequired();
        builder.HasIndex(node => node.ProjectId);
        builder.HasIndex(node => node.ParentId);
        builder.HasIndex(node => node.NodeType);
        builder.HasIndex(node => new { node.ProjectId, node.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE AND \"ParentId\" IS NULL")
            .HasDatabaseName("UX_WorkspaceNodes_ActiveRootName");
        builder.HasIndex(node => new { node.ProjectId, node.ParentId, node.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE AND \"ParentId\" IS NOT NULL")
            .HasDatabaseName("UX_WorkspaceNodes_ActiveSiblingName");
        builder.HasOne(node => node.Project).WithMany(project => project.WorkspaceNodes).HasForeignKey(node => node.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(node => node.Parent).WithMany(node => node.Children).HasForeignKey(node => node.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FileContentConfiguration : IEntityTypeConfiguration<FileContent>
{
    public void Configure(EntityTypeBuilder<FileContent> builder)
    {
        builder.HasQueryFilter(content => !content.Node.IsDeleted && !content.Node.Project.IsDeleted);
        builder.HasKey(content => content.NodeId);
        builder.Property(content => content.ContentHash).HasMaxLength(64);
        builder.Property(content => content.BinaryContent).HasColumnType("bytea");
        builder.Property(content => content.ConcurrencyToken).HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.HasOne(content => content.Node).WithOne(node => node.FileContent).HasForeignKey<FileContent>(content => content.NodeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(content => content.UpdatedBy).WithMany().HasForeignKey(content => content.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.HasQueryFilter(version => !version.IsDeleted && !version.Node.IsDeleted && !version.Node.Project.IsDeleted);
        builder.Property(version => version.ContentHash).HasMaxLength(64);
        builder.Property(version => version.BinaryContent).HasColumnType("bytea");
        builder.HasIndex(version => new { version.NodeId, version.VersionNumber }).IsUnique();
        builder.HasOne(version => version.Node).WithMany(node => node.Versions).HasForeignKey(version => version.NodeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(version => version.CreatedBy).WithMany().HasForeignKey(version => version.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
