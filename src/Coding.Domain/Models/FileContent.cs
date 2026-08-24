namespace Coding.Models;

public sealed class FileContent
{
    public Guid NodeId { get; set; }
    public WorkspaceNode Node { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public bool IsBinary { get; set; }
    public byte[]? BinaryContent { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString("N");
    public int VersionNumber { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedById { get; set; }
    public User UpdatedBy { get; set; } = null!;
}
