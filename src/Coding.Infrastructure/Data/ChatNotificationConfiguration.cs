using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted);
        builder.Property(item => item.Name).HasMaxLength(120);
        builder.Property(item => item.DirectKey).HasMaxLength(65);
        builder.HasIndex(item => item.DirectKey).IsUnique().HasFilter("\"DirectKey\" IS NOT NULL");
        builder.HasIndex(item => item.ProjectId).IsUnique().HasFilter("\"ProjectId\" IS NOT NULL AND \"Type\" = 1");
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.HasQueryFilter(item => !item.Conversation.IsDeleted);
        builder.HasIndex(item => new { item.ConversationId, item.UserId }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.ConversationId });
        builder.HasOne(item => item.Conversation).WithMany(item => item.Participants).HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        // Keep soft-deleted messages in the timeline so clients can render the
        // intentional "Message deleted" placeholder without exposing content.
        builder.HasQueryFilter(item => !item.Conversation.IsDeleted);
        builder.Property(item => item.Content).HasMaxLength(8000).IsRequired();
        builder.HasIndex(item => new { item.ConversationId, item.CreatedAt, item.ID });
        builder.HasOne(item => item.Conversation).WithMany(item => item.ChatMessages).HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Sender).WithMany().HasForeignKey(item => item.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MessageReadReceiptConfiguration : IEntityTypeConfiguration<MessageReadReceipt>
{
    public void Configure(EntityTypeBuilder<MessageReadReceipt> builder)
    {
        builder.HasQueryFilter(item => !item.Message.Conversation.IsDeleted);
        builder.HasKey(item => new { item.MessageId, item.UserId });
        builder.HasOne(item => item.Message).WithMany(item => item.ReadReceipts).HasForeignKey(item => item.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted && !item.Message.Conversation.IsDeleted);
        builder.Property(item => item.FileName).HasMaxLength(255).IsRequired();
        builder.Property(item => item.StoredName).HasMaxLength(80).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
        builder.HasIndex(item => item.MessageId);
        builder.HasOne(item => item.Message).WithMany(item => item.Attachments).HasForeignKey(item => item.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.UploadedBy).WithMany().HasForeignKey(item => item.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasQueryFilter(item => !item.IsDeleted);
        builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.RelatedEntityType).HasMaxLength(80);
        builder.HasIndex(item => new { item.UserId, item.IsRead, item.CreatedAt });
        builder.HasOne(item => item.User).WithMany(item => item.Notifications).HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.HasIndex(item => new { item.UserId, item.Type }).IsUnique();
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
