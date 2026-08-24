using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted);
        b.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(120);
        b.Property(x => x.Details).HasMaxLength(4000);
        b.HasIndex(x => new { x.State, x.CreatAt });
        b.HasIndex(x => new { x.TargetType, x.TargetId });
        b.HasIndex(x => new { x.ReporterId, x.TargetType, x.TargetId }).IsUnique().HasFilter("\"State\" IN ('Pending', 'Reviewing')");
        b.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AssignedModerator).WithMany().HasForeignKey(x => x.AssignedModeratorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ModerationActionRecordConfiguration : IEntityTypeConfiguration<ModerationActionRecord>
{
    public void Configure(EntityTypeBuilder<ModerationActionRecord> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Report.IsDeleted);
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.PreviousState).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.NewState).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(4000);
        b.HasIndex(x => new { x.ReportId, x.CreatAt });
        b.HasOne(x => x.Report).WithMany(x => x.Actions).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Moderator).WithMany().HasForeignKey(x => x.ModeratorId).OnDelete(DeleteBehavior.Restrict);
    }
}
