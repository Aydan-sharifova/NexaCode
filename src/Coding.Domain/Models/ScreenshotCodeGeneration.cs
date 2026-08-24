using Coding.Enums;

namespace Coding.Models;

public sealed class ScreenshotCodeGeneration : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Prompt { get; set; } = string.Empty;
    public string ImageFileName { get; set; } = string.Empty;
    public string ImageMediaType { get; set; } = string.Empty;
    public string ImageHash { get; set; } = string.Empty;
    public ScreenshotGenerationStatus Status { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public string AppTsx { get; set; } = string.Empty;
    public string StylesCss { get; set; } = string.Empty;
    public string PreviewHtml { get; set; } = string.Empty;
    public string TargetSnapshotsJson { get; set; } = "[]";
    public string? ModelProvider { get; set; }
    public string? ModelName { get; set; }
    public string? FailureReason { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}
