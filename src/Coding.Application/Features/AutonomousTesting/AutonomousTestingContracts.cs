using Coding.Enums;
using MediatR;

namespace Coding.Application.Features.AutonomousTesting;

public sealed record AutonomousTestIterationDto(
    Guid Id, int Number, AutonomousTestIterationOutcome Outcome, string SourceHash,
    string GeneratedTestSource, string? Stdout, string? Stderr, int? ExitCode,
    bool TimedOut, int DurationMs, string? FailureAnalysis, DateTime StartedAt, DateTime CompletedAt);

public sealed record AutonomousTestRunDto(
    Guid Id, Guid ProjectId, Guid WorkspaceNodeId, string Goal, string Language,
    AutonomousTestRunStatus Status, int MaximumIterations, int CompletedIterations,
    string? Analysis, string? FinalSummary, string? SuggestedFix, bool HasProposedFix, string? ProposedSource,
    string? ProposedSourceHash, string? ModelProvider, string? ModelName,
    DateTime StartedAt, DateTime? CompletedAt, DateTime? AppliedAt,
    Guid? AppliedFileVersionId, IReadOnlyList<AutonomousTestIterationDto> Iterations);

public sealed record AutonomousTestTimelineDto(IReadOnlyList<AutonomousTestRunDto> Items, int Total);

public sealed record StartAutonomousTestRunCommand(
    Guid ProjectId, Guid WorkspaceNodeId, string Goal, int MaximumIterations = 3)
    : IRequest<AutonomousTestRunDto>;

public sealed record GetAutonomousTestRunQuery(Guid ProjectId, Guid RunId) : IRequest<AutonomousTestRunDto>;
public sealed record ListAutonomousTestRunsQuery(Guid ProjectId, int Take = 30) : IRequest<AutonomousTestTimelineDto>;
public sealed record ApplyAutonomousTestFixCommand(Guid ProjectId, Guid RunId, bool Confirm)
    : IRequest<AutonomousTestRunDto>;
public sealed record RunAutonomousTestsAgainCommand(Guid ProjectId, Guid RunId, int MaximumIterations = 3)
    : IRequest<AutonomousTestRunDto>;

public interface IAutonomousTestAgentService
{
    Task<AutonomousTestRunDto> StartAsync(StartAutonomousTestRunCommand request, CancellationToken cancellationToken);
    Task<AutonomousTestRunDto> GetAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
    Task<AutonomousTestTimelineDto> ListAsync(Guid projectId, int take, CancellationToken cancellationToken);
    Task<AutonomousTestRunDto> ApplyAsync(Guid projectId, Guid runId, bool confirm, CancellationToken cancellationToken);
    Task<AutonomousTestRunDto> RunAgainAsync(Guid projectId, Guid runId, int maximumIterations, CancellationToken cancellationToken);
}
