using System.Text.Json;
using Coding.Enums;

namespace Coding.Models;

public sealed class ProjectPlan : Base
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Idea { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
    public JsonDocument PlanJson { get; set; } = JsonDocument.Parse("{}");
    public string PlanHash { get; set; } = string.Empty;
    public ProjectPlanStatus Status { get; set; }
    public int Version { get; set; } = 1;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Guid? CreatedProjectId { get; set; }
    public Project? CreatedProject { get; set; }
}

public sealed class ProjectMilestone : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ProjectIssue> Issues { get; set; } = [];
}

public sealed class ProjectIssue : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid MilestoneId { get; set; }
    public ProjectMilestone Milestone { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskPriority Priority { get; set; }
    public ProjectIssueStatus Status { get; set; }
    public int SortOrder { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ProjectTask> Tasks { get; set; } = [];
}
