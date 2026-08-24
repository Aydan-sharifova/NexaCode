using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Project.IsDeleted);
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(4000);
        builder.Property(item => item.Position).HasPrecision(28, 12);
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.Position });
        builder.HasOne(item => item.Project).WithMany(item => item.Tasks).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Issue).WithMany(item => item.Tasks).HasForeignKey(item => item.IssueId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class TaskAssigneeConfiguration : IEntityTypeConfiguration<TaskAssignee>
{
    public void Configure(EntityTypeBuilder<TaskAssignee> builder)
    {
        builder.HasQueryFilter(item => !item.Task.IsDeleted && !item.Task.Project.IsDeleted);
        builder.HasKey(item => new { item.TaskId, item.UserId });
        builder.HasOne(item => item.Task).WithMany(item => item.Assignees).HasForeignKey(item => item.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Task.IsDeleted && !item.Task.Project.IsDeleted);
        builder.Property(item => item.Content).HasMaxLength(4000).IsRequired();
        builder.HasIndex(item => new { item.TaskId, item.CreatedAt });
        builder.HasOne(item => item.Task).WithMany(item => item.Comments).HasForeignKey(item => item.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ActionType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.Metadata).HasColumnType("jsonb");
        builder.Property(item => item.IpAddress).HasMaxLength(64);
        builder.Property(item => item.UserAgent).HasMaxLength(512);
        builder.HasIndex(item => item.CreatedAt);
        builder.HasIndex(item => new { item.ProjectId, item.ActionType, item.EntityType, item.CreatedAt });
        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}
