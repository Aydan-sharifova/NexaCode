namespace Coding.Models;

public sealed class ChatMessage : Base
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }
    public ICollection<MessageReadReceipt> ReadReceipts { get; set; } = [];
    public ICollection<ChatAttachment> Attachments { get; set; } = [];
}
