namespace Coding.Models;

public sealed class ChatAttachment : Base
{
    public Guid MessageId { get; set; }
    public ChatMessage Message { get; set; } = null!;
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
}
