using Coding.Enums;

namespace Coding.Models;

public sealed class AutonomousTestRun : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid WorkspaceNodeId { get; set; }
    public WorkspaceNode WorkspaceNode { get; set; } = null!;
    public string Goal { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public AutonomousTestRunStatus Status { get; set; } = AutonomousTestRunStatus.Analyzing;
    public int MaximumIterations { get; set; }
    public int CompletedIterations { get; set; }
    public string OriginalSourceHash { get; set; } = string.Empty;
    public string OriginalConcurrencyToken { get; set; } = string.Empty;
    public string? Analysis { get; set; }
    public string? FinalSummary { get; set; }
    public string? ProposedSource { get; set; }
    public string? ProposedSourceHash { get; set; }
    public string? SuggestedFix { get; set; }
    public string? ModelProvider { get; set; }
    public string? ModelName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Guid? AppliedFileVersionId { get; set; }
    public ICollection<AutonomousTestIteration> Iterations { get; set; } = [];
}

public sealed class AutonomousTestIteration : Base
{
    public Guid RunId { get; set; }
    public AutonomousTestRun Run { get; set; } = null!;
    public int Number { get; set; }
    public AutonomousTestIterationOutcome Outcome { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public string GeneratedTestSource { get; set; } = string.Empty;
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public int? ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public int DurationMs { get; set; }
    public string? FailureAnalysis { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
