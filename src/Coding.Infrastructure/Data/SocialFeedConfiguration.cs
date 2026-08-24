using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class SocialPostConfiguration : IEntityTypeConfiguration<SocialPost>
{
    public void Configure(EntityTypeBuilder<SocialPost> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Author.IsDeleted);
        builder.Property(item => item.Content).HasMaxLength(10_000).IsRequired();
        builder.Property(item => item.CodeLanguage).HasMaxLength(50);
        builder.Property(item => item.ImageUrl).HasMaxLength(500);
        builder.HasIndex(item => new { item.CreatedAt, item.ID });
        builder.HasIndex(item => new { item.AuthorId, item.CreatedAt });
        builder.HasOne(item => item.Author).WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class SocialPostCommentConfiguration : IEntityTypeConfiguration<SocialPostComment>
{
    public void Configure(EntityTypeBuilder<SocialPostComment> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Author.IsDeleted && !item.Post.IsDeleted);
        builder.Property(item => item.Content).HasMaxLength(2_000).IsRequired();
        builder.HasIndex(item => new { item.PostId, item.CreatedAt });
        builder.HasOne(item => item.Post).WithMany(item => item.Comments).HasForeignKey(item => item.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Author).WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ParentComment).WithMany(item => item.Replies).HasForeignKey(item => item.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SocialPostReactionConfiguration : IEntityTypeConfiguration<SocialPostReaction>
{
    public void Configure(EntityTypeBuilder<SocialPostReaction> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Post.IsDeleted && !item.User.IsDeleted);
        builder.HasIndex(item => new { item.PostId, item.UserId }).IsUnique();
        builder.HasOne(item => item.Post).WithMany(item => item.Reactions).HasForeignKey(item => item.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SavedSocialPostConfiguration : IEntityTypeConfiguration<SavedSocialPost>
{
    public void Configure(EntityTypeBuilder<SavedSocialPost> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Post.IsDeleted && !item.User.IsDeleted);
        builder.HasIndex(item => new { item.UserId, item.PostId }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
        builder.HasOne(item => item.Post).WithMany(item => item.Saves).HasForeignKey(item => item.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SocialPostShareConfiguration : IEntityTypeConfiguration<SocialPostShare>
{
    public void Configure(EntityTypeBuilder<SocialPostShare> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Post.IsDeleted && !item.User.IsDeleted);
        builder.HasIndex(item => new { item.PostId, item.UserId }).IsUnique();
        builder.HasOne(item => item.Post).WithMany(item => item.Shares).HasForeignKey(item => item.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
