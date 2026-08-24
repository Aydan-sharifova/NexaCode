using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class SavedProjectConfiguration:IEntityTypeConfiguration<SavedProject>
{
    public void Configure(EntityTypeBuilder<SavedProject> builder)
    {
        builder.HasQueryFilter(x=>!x.Project.IsDeleted&&x.Project.IsPublic);
        builder.HasKey(x=>new{x.ProjectId,x.UserId});
        builder.HasIndex(x=>new{x.UserId,x.CreatedAt});
        builder.HasOne(x=>x.Project).WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
