using Coding.Enums;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasQueryFilter(project => !project.IsDeleted);
        builder.Property(project => project.Name).HasMaxLength(120).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(1000);
        builder.Property(project => project.DefaultLanguage).HasMaxLength(50);
        builder.HasIndex(project => project.OwnerId);
        builder.HasOne(project => project.Owner)
            .WithMany(user => user.OwnedProjects)
            .HasForeignKey(project => project.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasQueryFilter(member => !member.Project.IsDeleted);
        builder.HasIndex(member => new { member.ProjectId, member.UserId }).IsUnique();
        builder.HasOne(member => member.Project).WithMany(project => project.Members)
            .HasForeignKey(member => member.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(member => member.User).WithMany(user => user.ProjectMembers)
            .HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectInvitationConfiguration : IEntityTypeConfiguration<ProjectInvitation>
{
    public void Configure(EntityTypeBuilder<ProjectInvitation> builder)
    {
        builder.HasQueryFilter(invitation => !invitation.Project.IsDeleted && !invitation.IsDeleted);
        builder.Property(invitation => invitation.Email).HasMaxLength(254).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.ProjectId, invitation.Email })
            .HasFilter("\"Status\" = 0").IsUnique();
        builder.HasOne(invitation => invitation.Project).WithMany(project => project.Invitations)
            .HasForeignKey(invitation => invitation.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(invitation => invitation.InvitedBy).WithMany()
            .HasForeignKey(invitation => invitation.InvitedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectFolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder) => builder.HasQueryFilter(folder => !folder.Project.IsDeleted && !folder.IsDeleted);
}

public sealed class ProjectCommitConfiguration : IEntityTypeConfiguration<GitCommit>
{
    public void Configure(EntityTypeBuilder<GitCommit> builder) => builder.HasQueryFilter(commit => !commit.Project.IsDeleted && !commit.IsDeleted);
}

public sealed class ProjectAiRequestConfiguration : IEntityTypeConfiguration<AIRequest>
{
    public void Configure(EntityTypeBuilder<AIRequest> builder) => builder.HasQueryFilter(request => !request.Project.IsDeleted && !request.IsDeleted);
}

public sealed class ProjectAiResponseConfiguration : IEntityTypeConfiguration<AIResponse>
{
    public void Configure(EntityTypeBuilder<AIResponse> builder) =>
        builder.HasQueryFilter(response => !response.IsDeleted && !response.AIRequest.IsDeleted && !response.AIRequest.Project.IsDeleted);
}

public sealed class ProjectFileItemConfiguration : IEntityTypeConfiguration<FileItem>
{
    public void Configure(EntityTypeBuilder<FileItem> builder) =>
        builder.HasQueryFilter(file => !file.IsDeleted && !file.Folder.IsDeleted && !file.Folder.Project.IsDeleted);
}

public sealed class ProjectCodeHistoryConfiguration : IEntityTypeConfiguration<CodeHistory>
{
    public void Configure(EntityTypeBuilder<CodeHistory> builder) =>
        builder.HasQueryFilter(history => !history.IsDeleted && !history.FileItem.IsDeleted && !history.FileItem.Folder.IsDeleted && !history.FileItem.Folder.Project.IsDeleted);
}
