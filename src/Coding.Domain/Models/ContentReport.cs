using Coding.Enums;

namespace Coding.Models;

public sealed class ContentReport : Base
{
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public ModerationReportState State { get; set; } = ModerationReportState.Pending;
    public Guid? AssignedModeratorId { get; set; }
    public User? AssignedModerator { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ICollection<ModerationActionRecord> Actions { get; set; } = [];
}

public sealed class ModerationActionRecord : Base
{
    public Guid ReportId { get; set; }
    public ContentReport Report { get; set; } = null!;
    public Guid ModeratorId { get; set; }
    public User Moderator { get; set; } = null!;
    public ModerationActionType Action { get; set; }
    public ModerationReportState PreviousState { get; set; }
    public ModerationReportState NewState { get; set; }
    public string Note { get; set; } = string.Empty;
}
