using Coding.Enums;

namespace Coding.Models;

public sealed class DebuggingIncident : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public Guid? WorkspaceNodeId { get; set; }
    public WorkspaceNode? WorkspaceNode { get; set; }
    public DebuggingIncidentKind Kind { get; set; }
    public DebuggingIncidentStatus Status { get; set; }
    public string Language { get; set; } = string.Empty;
    public string ErrorSummary { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public int? ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public int DurationMs { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public string? RootCause { get; set; }
    public string? LikelyRegression { get; set; }
    public string? SuggestedFix { get; set; }
    public string? RelevantCommitSha { get; set; }
    public DebugEvidenceConfidence? RegressionConfidence { get; set; }
    public string? ModelProvider { get; set; }
    public string? ModelName { get; set; }
    public ICollection<DebuggingEvidence> Evidence { get; set; } = [];
    public Guid ExecutionObservationId { get; set; }
    public DebuggingExecutionObservation ExecutionObservation { get; set; } = null!;
}

public sealed class DebuggingExecutionObservation : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? WorkspaceNodeId { get; set; }
    public WorkspaceNode? WorkspaceNode { get; set; }
    public DebuggingIncidentKind Kind { get; set; }
    public string Language { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public int? ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public int DurationMs { get; set; }
    public DateTime ExecutedAt { get; set; }
    public DebuggingIncident? Incident { get; set; }
}

public sealed class DebuggingEvidence : Base
{
    public Guid IncidentId { get; set; }
    public DebuggingIncident Incident { get; set; } = null!;
    public DebugEvidenceKind Kind { get; set; }
    public DebugEvidenceConfidence Confidence { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid? WorkspaceNodeId { get; set; }
    public Guid? FileVersionId { get; set; }
    public string? CommitSha { get; set; }
    public DateTime? EvidenceAt { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
}
