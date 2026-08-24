using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class DebuggingIncidentConfiguration : IEntityTypeConfiguration<DebuggingIncident>
{
    public void Configure(EntityTypeBuilder<DebuggingIncident> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.RegressionConfidence).HasConversion<int>();
        builder.Property(x => x.Language).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorSummary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.StackTrace).HasMaxLength(16000);
        builder.Property(x => x.Stdout).HasMaxLength(16000);
        builder.Property(x => x.Stderr).HasMaxLength(16000);
        builder.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RootCause).HasMaxLength(4000);
        builder.Property(x => x.LikelyRegression).HasMaxLength(2000);
        builder.Property(x => x.SuggestedFix).HasMaxLength(4000);
        builder.Property(x => x.RelevantCommitSha).HasMaxLength(40);
        builder.Property(x => x.ModelProvider).HasMaxLength(50);
        builder.Property(x => x.ModelName).HasMaxLength(100);
        builder.HasIndex(x => new { x.ProjectId, x.OccurredAt });
        builder.HasIndex(x => x.ExecutionObservationId).IsUnique();
        builder.HasOne(x => x.Project).WithMany(x => x.DebuggingIncidents).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkspaceNode).WithMany().HasForeignKey(x => x.WorkspaceNodeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ExecutionObservation).WithOne(x => x.Incident).HasForeignKey<DebuggingIncident>(x => x.ExecutionObservationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DebuggingExecutionObservationConfiguration : IEntityTypeConfiguration<DebuggingExecutionObservation>
{
    public void Configure(EntityTypeBuilder<DebuggingExecutionObservation> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Language).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.WorkspaceNodeId, x.ExecutedAt });
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkspaceNode).WithMany().HasForeignKey(x => x.WorkspaceNodeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class DebuggingEvidenceConfiguration : IEntityTypeConfiguration<DebuggingEvidence>
{
    public void Configure(EntityTypeBuilder<DebuggingEvidence> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Incident.IsDeleted && !x.Incident.Project.IsDeleted);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Confidence).HasConversion<int>();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CommitSha).HasMaxLength(40);
        builder.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.IncidentId, x.Fingerprint }).IsUnique();
        builder.HasOne(x => x.Incident).WithMany(x => x.Evidence).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
    }
}
