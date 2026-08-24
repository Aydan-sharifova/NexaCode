using Coding.Domain.Services;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class ProjectPlanPolicyTests
{
    [Fact]
    public void Accepts_complete_bounded_plan()
    {
        ProjectPlanPolicy.Validate(Valid()).Should().BeEmpty();
    }

    [Fact]
    public void Requires_all_seven_sections()
    {
        var plan = Valid() with { Sections = new("Architecture", "Database", "API", "Frontend", "Authentication", "", "Deployment") };
        ProjectPlanPolicy.Validate(plan).Should().Contain(x => x.Contains("testing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_unbounded_bulk_creation()
    {
        var tasks = Enumerable.Range(1, ProjectPlanPolicy.MaximumTasksPerIssue + 1).Select(i => new ProjectPlanTask($"Task {i}", "Description", ProjectTaskPriority.Medium)).ToArray();
        var issue = new ProjectPlanIssue("Issue", "Description", ProjectTaskPriority.High, tasks);
        var plan = Valid() with { Milestones = [new("Milestone", "Description", [issue])] };
        ProjectPlanPolicy.Validate(plan).Should().Contain(x => x.Contains("tasks", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectPlanBlueprint Valid() => new(
        "Food Delivery", "A bounded delivery platform.", "C#",
        new("Layered architecture", "PostgreSQL entities", "REST API", "React frontend", "JWT authentication", "Unit and integration tests", "Container deployment"),
        [new("Foundation", "Establish the core.", [new("Authentication", "Implement authentication.", ProjectTaskPriority.High, [new("Create login API", "Add validation and tests.", ProjectTaskPriority.High)])])]);
}
