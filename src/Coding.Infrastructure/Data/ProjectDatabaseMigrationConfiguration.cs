using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ProjectDatabaseMigrationConfiguration : IEntityTypeConfiguration<ProjectDatabaseMigration>
{
    public void Configure(EntityTypeBuilder<ProjectDatabaseMigration> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProposedSchemaJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DdlPreview).HasMaxLength(12000).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.ProjectId, x.CreatAt });
        builder.HasOne(x => x.Project).WithMany(x => x.DatabaseMigrations).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
