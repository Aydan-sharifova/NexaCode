namespace Coding.Models;

public sealed class FileVersion : Base
{
    public Guid NodeId { get; set; }
    public WorkspaceNode Node { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsBinary { get; set; }
    public byte[]? BinaryContent { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
}
