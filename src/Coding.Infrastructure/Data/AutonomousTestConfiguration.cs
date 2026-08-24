using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class AutonomousTestRunConfiguration : IEntityTypeConfiguration<AutonomousTestRun>
{
    public void Configure(EntityTypeBuilder<AutonomousTestRun> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Goal).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OriginalSourceHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OriginalConcurrencyToken).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Analysis).HasMaxLength(4000);
        builder.Property(x => x.FinalSummary).HasMaxLength(4000);
        builder.Property(x => x.ProposedSource).HasColumnType("text");
        builder.Property(x => x.ProposedSourceHash).HasMaxLength(64);
        builder.Property(x => x.SuggestedFix).HasMaxLength(4000);
        builder.Property(x => x.ModelProvider).HasMaxLength(50);
        builder.Property(x => x.ModelName).HasMaxLength(100);
        builder.HasIndex(x => new { x.ProjectId, x.StartedAt });
        builder.HasOne(x => x.Project).WithMany(x => x.AutonomousTestRuns).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkspaceNode).WithMany().HasForeignKey(x => x.WorkspaceNodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AutonomousTestIterationConfiguration : IEntityTypeConfiguration<AutonomousTestIteration>
{
    public void Configure(EntityTypeBuilder<AutonomousTestIteration> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Run.IsDeleted && !x.Run.Project.IsDeleted);
        builder.Property(x => x.Outcome).HasConversion<int>();
        builder.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.GeneratedTestSource).HasColumnType("text").IsRequired();
        builder.Property(x => x.Stdout).HasMaxLength(32000);
        builder.Property(x => x.Stderr).HasMaxLength(32000);
        builder.Property(x => x.FailureAnalysis).HasMaxLength(4000);
        builder.HasIndex(x => new { x.RunId, x.Number }).IsUnique();
        builder.HasOne(x => x.Run).WithMany(x => x.Iterations).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
