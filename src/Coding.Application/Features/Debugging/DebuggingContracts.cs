using Coding.Enums;
using MediatR;

namespace Coding.Application.Features.Debugging;

public sealed record DebugEvidenceDto(Guid Id, DebugEvidenceKind Kind, DebugEvidenceConfidence Confidence, string Label, string Summary, Guid? WorkspaceNodeId, Guid? FileVersionId, string? CommitSha, DateTime? EvidenceAt);
public sealed record DebuggingIncidentDto(Guid Id, Guid ProjectId, Guid? WorkspaceNodeId, DebuggingIncidentKind Kind, DebuggingIncidentStatus Status, string Language, string ErrorSummary, string? StackTrace, string? Stdout, string? Stderr, int? ExitCode, bool TimedOut, int DurationMs, DateTime OccurredAt, DateTime? AnalyzedAt, string? RootCause, string? LikelyRegression, string? SuggestedFix, string? RelevantCommitSha, DebugEvidenceConfidence? RegressionConfidence, string? ModelProvider, string? ModelName, IReadOnlyList<DebugEvidenceDto> Evidence);
public sealed record DebuggingTimelineDto(IReadOnlyList<DebuggingIncidentDto> Items, int Total);
public sealed record DebugExecutionCapture(Guid ProjectId, Guid UserId, Guid? WorkspaceNodeId, DebuggingIncidentKind Kind, string Language, string Source, int? ExitCode, string Stdout, string Stderr, bool TimedOut, int DurationMs);

public sealed record ListDebuggingTimelineQuery(Guid ProjectId, int Take = 30) : IRequest<DebuggingTimelineDto>;
public sealed record GetDebuggingIncidentQuery(Guid ProjectId, Guid IncidentId) : IRequest<DebuggingIncidentDto>;
public sealed record AnalyzeDebuggingIncidentCommand(Guid ProjectId, Guid IncidentId, bool UseModel = true) : IRequest<DebuggingIncidentDto>;

public interface IDebuggingTimelineService
{
    Task<Guid?> CaptureFailureAsync(DebugExecutionCapture capture, CancellationToken cancellationToken);
    Task<DebuggingTimelineDto> ListAsync(Guid projectId, int take, CancellationToken cancellationToken);
    Task<DebuggingIncidentDto> GetAsync(Guid projectId, Guid incidentId, CancellationToken cancellationToken);
    Task<DebuggingIncidentDto> AnalyzeAsync(Guid projectId, Guid incidentId, bool useModel, CancellationToken cancellationToken);
}
