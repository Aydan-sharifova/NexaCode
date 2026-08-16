using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class CodingSessionConfiguration : IEntityTypeConfiguration<CodingSession>
{
    public void Configure(EntityTypeBuilder<CodingSession> builder)
    {
        builder.HasQueryFilter(x => !x.File.IsDeleted && !x.File.Project.IsDeleted);
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.StartAt });
        builder.HasIndex(x => new { x.ProjectId, x.StartAt });
        builder.HasIndex(x => new { x.FileId, x.StartAt });
        builder.HasIndex(x => new { x.UserId, x.FileId, x.EndAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
    }
}
