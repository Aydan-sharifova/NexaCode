using Coding.Enums;

namespace Coding.Models;

public sealed class AiUiGeneration : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Prompt { get; set; } = string.Empty;
    public bool IncludeSampleData { get; set; }
    public ScreenshotGenerationStatus Status { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public string PreviewHtml { get; set; } = string.Empty;
    public string FilesJson { get; set; } = "[]";
    public string TargetSnapshotsJson { get; set; } = "[]";
    public string? ModelProvider { get; set; }
    public string? ModelName { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}
