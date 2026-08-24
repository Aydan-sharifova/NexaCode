using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ProjectPlanConfiguration : IEntityTypeConfiguration<ProjectPlan>
{
    public void Configure(EntityTypeBuilder<ProjectPlan> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.User.IsDeleted);
        builder.Property(x => x.Idea).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DefaultLanguage).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PlanJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PlanHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedProject).WithMany().HasForeignKey(x => x.CreatedProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder }).IsUnique();
        builder.HasOne(x => x.Project).WithMany(x => x.Milestones).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectIssueConfiguration : IEntityTypeConfiguration<ProjectIssue>
{
    public void Configure(EntityTypeBuilder<ProjectIssue> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted && !x.Milestone.IsDeleted);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.MilestoneId, x.SortOrder }).IsUnique();
        builder.HasOne(x => x.Project).WithMany(x => x.Issues).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Milestone).WithMany(x => x.Issues).HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.Cascade);
    }
}
