using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ProjectDeploymentConfiguration : IEntityTypeConfiguration<ProjectDeployment>
{
    public void Configure(EntityTypeBuilder<ProjectDeployment> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CommitSha).HasMaxLength(64);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
        builder.HasIndex(x => x.ProjectId).HasFilter("\"IsActive\" = TRUE AND \"IsDeleted\" = FALSE").IsUnique();
        builder.HasOne(x => x.Project).WithMany(x => x.Deployments).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DeployedBy).WithMany().HasForeignKey(x => x.DeployedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectDeploymentFileConfiguration : IEntityTypeConfiguration<ProjectDeploymentFile>
{
    public void Configure(EntityTypeBuilder<ProjectDeploymentFile> builder)
    {
        builder.HasQueryFilter(x => !x.Deployment.IsDeleted && !x.Deployment.Project.IsDeleted);
        builder.HasKey(x => new { x.DeploymentId, x.Path });
        builder.Property(x => x.Path).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.Deployment).WithMany(x => x.Files).HasForeignKey(x => x.DeploymentId).OnDelete(DeleteBehavior.Cascade);
    }
}
