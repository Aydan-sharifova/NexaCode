using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class PullRequestConfiguration : IEntityTypeConfiguration<PullRequest>
{
    public void Configure(EntityTypeBuilder<PullRequest> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Project.IsDeleted);
        builder.Property(item => item.Title).HasMaxLength(180).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(5000);
        builder.Property(item => item.SourceBranch).HasMaxLength(200).IsRequired();
        builder.Property(item => item.TargetBranch).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SourceHeadSha).HasMaxLength(40).IsRequired();
        builder.Property(item => item.TargetHeadSha).HasMaxLength(40).IsRequired();
        builder.Property(item => item.MergeCommitSha).HasMaxLength(40);
        builder.Property(item => item.TestSummary).HasMaxLength(1000);
        builder.HasIndex(item => new { item.ProjectId, item.Number }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.UpdatedAt });
        builder.HasIndex(item => new { item.ProjectId, item.SourceBranch })
            .HasFilter("\"Status\" = 0 AND \"IsDeleted\" = false").IsUnique();
        builder.HasOne(item => item.Project).WithMany(item => item.PullRequests)
            .HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Author).WithMany(item => item.AuthoredPullRequests)
            .HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MergedBy).WithMany()
            .HasForeignKey(item => item.MergedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PullRequestReviewConfiguration : IEntityTypeConfiguration<PullRequestReview>
{
    public void Configure(EntityTypeBuilder<PullRequestReview> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.PullRequest.IsDeleted && !item.PullRequest.Project.IsDeleted);
        builder.Property(item => item.Body).HasMaxLength(3000);
        builder.Property(item => item.ReviewedSourceSha).HasMaxLength(40).IsRequired();
        builder.HasIndex(item => new { item.PullRequestId, item.ReviewerId }).IsUnique();
        builder.HasOne(item => item.PullRequest).WithMany(item => item.Reviews)
            .HasForeignKey(item => item.PullRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Reviewer).WithMany(item => item.PullRequestReviews)
            .HasForeignKey(item => item.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PullRequestCommentConfiguration : IEntityTypeConfiguration<PullRequestComment>
{
    public void Configure(EntityTypeBuilder<PullRequestComment> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.PullRequest.IsDeleted && !item.PullRequest.Project.IsDeleted);
        builder.Property(item => item.Body).HasMaxLength(5000).IsRequired();
        builder.Property(item => item.FilePath).HasMaxLength(500);
        builder.Property(item => item.CommitSha).HasMaxLength(40);
        builder.HasIndex(item => new { item.PullRequestId, item.IsResolved, item.IsBlocking });
        builder.HasOne(item => item.PullRequest).WithMany(item => item.Comments)
            .HasForeignKey(item => item.PullRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Author).WithMany(item => item.PullRequestComments)
            .HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ResolvedBy).WithMany()
            .HasForeignKey(item => item.ResolvedById).OnDelete(DeleteBehavior.SetNull);
    }
}
