using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class KnowledgeGraphSnapshotConfiguration : IEntityTypeConfiguration<KnowledgeGraphSnapshot>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphSnapshot> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.SourceFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
        builder.HasIndex(x => x.ProjectId).IsUnique().HasFilter("\"IsCurrent\"").HasDatabaseName("UX_KnowledgeGraphSnapshots_CurrentProject");
        builder.HasOne(x => x.Project).WithMany(x => x.KnowledgeGraphSnapshots).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class KnowledgeGraphNodeConfiguration : IEntityTypeConfiguration<KnowledgeGraphNode>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphNode> builder)
    {
        builder.HasQueryFilter(x => !x.Snapshot.IsDeleted && !x.Snapshot.Project.IsDeleted);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Key).HasMaxLength(700).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(1000);
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.SnapshotId, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.SnapshotId, x.Kind });
        builder.HasIndex(x => new { x.SnapshotId, x.SourceFileId });
        builder.HasOne(x => x.Snapshot).WithMany(x => x.Nodes).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SourceFile).WithMany().HasForeignKey(x => x.SourceFileId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class KnowledgeGraphEdgeConfiguration : IEntityTypeConfiguration<KnowledgeGraphEdge>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphEdge> builder)
    {
        builder.HasQueryFilter(x => !x.Snapshot.IsDeleted && !x.Snapshot.Project.IsDeleted);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Confidence).HasPrecision(4, 3);
        builder.Property(x => x.Evidence).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.SnapshotId, x.FromNodeId, x.ToNodeId, x.Kind }).IsUnique();
        builder.HasIndex(x => new { x.SnapshotId, x.ToNodeId });
        builder.HasOne(x => x.Snapshot).WithMany(x => x.Edges).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.FromNode).WithMany(x => x.Outgoing).HasForeignKey(x => x.FromNodeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ToNode).WithMany(x => x.Incoming).HasForeignKey(x => x.ToNodeId).OnDelete(DeleteBehavior.Cascade);
    }
}
