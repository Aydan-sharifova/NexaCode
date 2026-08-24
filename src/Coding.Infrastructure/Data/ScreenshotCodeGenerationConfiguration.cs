using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ScreenshotCodeGenerationConfiguration : IEntityTypeConfiguration<ScreenshotCodeGeneration>
{
    public void Configure(EntityTypeBuilder<ScreenshotCodeGeneration> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Project.IsDeleted);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Prompt).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ImageFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ImageMediaType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ImageHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Analysis).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.AppTsx).HasColumnType("text").IsRequired();
        builder.Property(x => x.StylesCss).HasColumnType("text").IsRequired();
        builder.Property(x => x.PreviewHtml).HasColumnType("text").IsRequired();
        builder.Property(x => x.TargetSnapshotsJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.ModelProvider).HasMaxLength(50);
        builder.Property(x => x.ModelName).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(4000);
        builder.HasIndex(x => new { x.ProjectId, x.GeneratedAt });
        builder.HasOne(x => x.Project).WithMany(x => x.ScreenshotCodeGenerations).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
