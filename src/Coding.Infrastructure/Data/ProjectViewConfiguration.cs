using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coding.Data;
public sealed class ProjectViewConfiguration:IEntityTypeConfiguration<ProjectView>
{
    public void Configure(EntityTypeBuilder<ProjectView> b){b.HasQueryFilter(x=>!x.Project.IsDeleted);b.HasKey(x=>new{x.ProjectId,x.UserId,x.ViewedOn});b.HasIndex(x=>new{x.ProjectId,x.ViewedAt});b.HasOne(x=>x.Project).WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);}
}
