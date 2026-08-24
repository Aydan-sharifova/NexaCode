using Coding.Enums;

namespace Coding.Models;

public sealed class ProjectTask : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? IssueId { get; set; }
    public ProjectIssue? Issue { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskStatus Status { get; set; }
    public ProjectTaskPriority Priority { get; set; }
    public decimal Position { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<TaskAssignee> Assignees { get; set; } = [];
    public ICollection<TaskComment> Comments { get; set; } = [];
}
