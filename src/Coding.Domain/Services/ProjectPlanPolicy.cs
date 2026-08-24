using Coding.Enums;

namespace Coding.Domain.Services;

public sealed record ProjectPlanBlueprint(
    string Title,
    string Summary,
    string DefaultLanguage,
    ProjectPlanSections Sections,
    IReadOnlyList<ProjectPlanMilestone> Milestones);
public sealed record ProjectPlanSections(string Architecture, string Database, string Api, string Frontend, string Authentication, string Testing, string Deployment);
public sealed record ProjectPlanMilestone(string Title, string Description, IReadOnlyList<ProjectPlanIssue> Issues);
public sealed record ProjectPlanIssue(string Title, string Description, ProjectTaskPriority Priority, IReadOnlyList<ProjectPlanTask> Tasks);
public sealed record ProjectPlanTask(string Title, string Description, ProjectTaskPriority Priority);

public static class ProjectPlanPolicy
{
    public const int MaximumMilestones = 8;
    public const int MaximumIssuesPerMilestone = 10;
    public const int MaximumTasksPerIssue = 10;
    public const int MaximumTotalTasks = 100;

    public static IReadOnlyList<string> Validate(ProjectPlanBlueprint? plan)
    {
        var errors = new List<string>();
        if (plan is null) return ["The model did not return a project plan."];
        Text(plan.Title, 120, "title", errors); Text(plan.Summary, 2000, "summary", errors); Text(plan.DefaultLanguage, 50, "default language", errors);
        if (plan.Sections is null) errors.Add("All seven planning sections are required.");
        else
        {
            Text(plan.Sections.Architecture, 4000, "architecture", errors); Text(plan.Sections.Database, 4000, "database", errors);
            Text(plan.Sections.Api, 4000, "API", errors); Text(plan.Sections.Frontend, 4000, "frontend", errors);
            Text(plan.Sections.Authentication, 4000, "authentication", errors); Text(plan.Sections.Testing, 4000, "testing", errors);
            Text(plan.Sections.Deployment, 4000, "deployment", errors);
        }
        if (plan.Milestones is null || plan.Milestones.Count is < 1 or > MaximumMilestones) errors.Add($"The plan must contain 1-{MaximumMilestones} milestones.");
        else foreach (var milestone in plan.Milestones)
        {
            Text(milestone.Title, 160, "milestone title", errors); Text(milestone.Description, 2000, "milestone description", errors);
            if (milestone.Issues is null || milestone.Issues.Count is < 1 or > MaximumIssuesPerMilestone) errors.Add($"Every milestone must contain 1-{MaximumIssuesPerMilestone} issues.");
            else foreach (var issue in milestone.Issues)
            {
                Text(issue.Title, 200, "issue title", errors); Text(issue.Description, 4000, "issue description", errors);
                if (!Enum.IsDefined(issue.Priority)) errors.Add("Issue priority is invalid.");
                if (issue.Tasks is null || issue.Tasks.Count is < 1 or > MaximumTasksPerIssue) errors.Add($"Every issue must contain 1-{MaximumTasksPerIssue} tasks.");
                else foreach (var task in issue.Tasks) { Text(task.Title, 200, "task title", errors); Text(task.Description, 4000, "task description", errors); if (!Enum.IsDefined(task.Priority)) errors.Add("Task priority is invalid."); }
            }
        }
        if (plan.Milestones?.SelectMany(x => x.Issues ?? []).SelectMany(x => x.Tasks ?? []).Count() > MaximumTotalTasks) errors.Add($"The plan may contain at most {MaximumTotalTasks} tasks.");
        return errors.Distinct().ToArray();
    }

    private static void Text(string? value, int maximum, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum) errors.Add($"The {name} is required and must not exceed {maximum} characters.");
    }
}
