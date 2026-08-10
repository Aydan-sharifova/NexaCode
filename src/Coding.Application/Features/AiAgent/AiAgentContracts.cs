using System.Text.Json;
using Coding.Enums;
using Coding.Models;

namespace Coding.Application.Features.AiAgent;

/// <summary>
/// Public read model of an agent run returned to API consumers.
/// </summary>
public sealed record AiAgentRunSummary(
    Guid Id,
    Guid ProjectId,
    AiAgentMode Mode,
    AiAgentStatus Status,
    string Goal,
    int CurrentStep,
    int MaximumSteps,
    string? ModelName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

/// <summary>
/// One step in the agent timeline.
/// </summary>
public sealed record AiAgentStepSummary(
    Guid Id,
    int StepNumber,
    AiAgentStepType StepType,
    string? InputSummary,
    string? OutputSummary,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

/// <summary>
/// One tool call attempt.
/// </summary>
public sealed record AiToolCallSummary(
    Guid Id,
    Guid? StepId,
    string ToolName,
    AiToolRiskLevel RiskLevel,
    AiApprovalStatus ApprovalStatus,
    string ArgumentsJson,
    string? ResultSummary,
    string? ErrorMessage,
    DateTime RequestedAt,
    DateTime? ApprovedAt,
    DateTime? ExecutedAt,
    string IdempotencyKey);

/// <summary>
/// One pending or resolved approval request.
/// </summary>
public sealed record AiApprovalRequestSummary(
    Guid Id,
    Guid AgentRunId,
    Guid ToolCallId,
    AiApprovalStatus Status,
    string ArgumentsHash,
    DateTime ExpiresAt,
    DateTime? RespondedAt);

/// <summary>
/// Structured plan returned by the planner.
/// </summary>
public sealed record AiAgentPlan(
    string Summary,
    AiToolRiskLevel RiskLevel,
    IReadOnlyList<AiAgentPlanStep> Steps,
    IReadOnlyList<string> FilesLikelyAffected,
    IReadOnlyList<string> TestStrategy,
    IReadOnlyList<string> SecurityConsiderations);

public sealed record AiAgentPlanStep(
    int Order,
    string Description,
    IReadOnlyList<string> Tools,
    bool RequiresApproval);

/// <summary>
/// Reviewer finding returned from the post-run review stage.
/// </summary>
public sealed record AiReviewFindingSummary(
    Guid Id,
    AiReviewSeverity Severity,
    string Category,
    string? FilePath,
    int? Line,
    string Message,
    string? Recommendation,
    DateTime CreatedAt);

/// <summary>
/// Outcome reported to the user once a run terminates.
/// </summary>
public sealed record AiAgentFinalReport(
    Guid RunId,
    AiAgentStatus Status,
    string? Summary,
    IReadOnlyList<AiReviewFindingSummary> Findings,
    IReadOnlyList<string> ChangedFiles,
    string? ErrorMessage);

/// <summary>
/// Context used by the orchestrator to gate execution. The orchestrator must
/// produce this before invoking any AI provider, planner, or tool.
/// </summary>
public sealed record AiAgentRunLimits(
    int MaximumSteps,
    int MaximumToolCalls,
    TimeSpan MaximumDuration,
    int MaximumContextCharacters,
    int MaximumPatchBytes,
    int MaximumModifiedFiles,
    TimeSpan ApprovalLifetime);

/// <summary>
/// Inputs the orchestrator needs to start a run. Project authorization is
/// verified separately by the application layer.
/// </summary>
public sealed record AiAgentStartRequest(
    Guid ProjectId,
    AiAgentMode Mode,
    string Goal,
    Guid? ConversationId = null,
    Guid? CurrentFileId = null,
    string? SelectedCode = null,
    IReadOnlyList<Guid>? ReferencedFileIds = null);

/// <summary>
/// Marker interface for any AI tool input payload. Each tool defines its own
/// concrete record implementing this marker so the registry can validate
/// arguments before execution.
/// </summary>
public interface IAiToolInput
{
}

/// <summary>
/// Marker interface for AI tool results. Concrete records carry the
/// tool-specific payload and are responsible for redacting secrets.
/// </summary>
public interface IAiToolResult
{
    /// <summary>Short, client-safe description of the result.</summary>
    string Summary { get; }

    /// <summary>Full result JSON if the tool produces structured output.</summary>
    string? Json => null;
}

/// <summary>
/// Metadata describing a single AI tool. The registry exposes this to the
/// planner so model output can be validated before any execution.
/// </summary>
public sealed record AiToolDescriptor(
    string Name,
    string Description,
    AiToolRiskLevel RiskLevel,
    IReadOnlySet<AiAgentMode> AllowedModes,
    IReadOnlySet<ProjectRole> RequiredRoles,
    Type InputType);

public interface IAiTool
{
    AiToolDescriptor Descriptor { get; }

    /// <summary>
    /// Execute the tool. Implementations must enforce all authorization and
    /// redaction guarantees themselves; the orchestrator validates the
    /// envelope but trusts the tool to behave.
    /// </summary>
    Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken);
}

public interface IAiToolRegistry
{
    AiToolDescriptor Describe(string toolName);
    bool TryGet(string toolName, out IAiTool tool);
    IReadOnlyCollection<AiToolDescriptor> ListAll();
}

/// <summary>
/// Authorizes a proposed tool call inside a specific run. The orchestrator
/// must invoke this before dispatching any tool call.
/// </summary>
public interface IAiToolAuthorizationService
{
    Task<AiAuthorizationDecision> AuthorizeAsync(AiToolCall call, AiAgentRun run, CancellationToken cancellationToken);
}

public sealed record AiAuthorizationDecision(
    bool IsAllowed,
    string? Reason,
    IReadOnlyList<string> MissingChecks)
{
    public static AiAuthorizationDecision Allow() => new(true, null, []);
    public static AiAuthorizationDecision Deny(string reason, params string[] missingChecks) =>
        new(false, reason, missingChecks);
}

/// <summary>
/// Determines whether a tool call needs explicit user approval and whether
/// an existing approval can be reused.
/// </summary>
public interface IAiToolApprovalPolicy
{
    bool RequiresApproval(AiToolDescriptor descriptor);

    bool CanAutoApproveLowRisk(AiAgentRun run, AiToolDescriptor descriptor);

    /// <summary>
    /// Returns true when the provided approval is still valid for the given
    /// tool call: same run, same tool, same normalized arguments hash, not
    /// expired, and not rejected.
    /// </summary>
    bool IsApprovalValid(AiApprovalStatus status, string approvalHash, string callHash, DateTime expiresAt, DateTime nowUtc);
}

/// <summary>
/// Build/inspect patches. The orchestrator delegates all file modification
/// to this interface so file operations stay inside the application layer.
/// </summary>
public interface IAiPatchService
{
    Task<AiPatchPreview> PreviewAsync(Guid agentRunId, AiPatch patch, CancellationToken cancellationToken);
    Task<AiPatchApplyResult> ApplyAsync(Guid agentRunId, Guid patchId, Guid userId, CancellationToken cancellationToken);
    Task<AiPatchApplyResult> RollbackAsync(Guid agentRunId, Guid patchId, Guid userId, CancellationToken cancellationToken);
}

public sealed record AiPatchPreview(
    Guid PatchId,
    string FilePath,
    string? UnifiedDiff,
    string OriginalContentHash,
    string ProposedContentHash,
    int AddedLineCount,
    int RemovedLineCount,
    bool IsStale);

public sealed record AiPatchApplyResult(
    Guid PatchId,
    string FilePath,
    bool Applied,
    Guid? FileVersionId,
    string? ErrorMessage);

/// <summary>
/// Generates a structured plan from the user goal and authorized context.
/// </summary>
public interface IAiPlanner
{
    Task<AiAgentPlan> PlanAsync(AiAgentRun run, AiAgentRepositoryContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Reviews the final state of an agent run and produces structured findings
/// without modifying any project file.
/// </summary>
public interface IAiReviewer
{
    Task<IReadOnlyList<AiReviewFindingSummary>> ReviewAsync(AiAgentRun run, AiAgentFinalReport draft, CancellationToken cancellationToken);
}

/// <summary>
/// Enforces aggregate counters and time limits on a run.
/// </summary>
public interface IAiRunLimitService
{
    bool HasExceededSteps(AiAgentRun run);
    bool HasExceededToolCalls(AiAgentRun run);
    bool HasExceededDuration(AiAgentRun run);
    AiAgentRunLimits Defaults { get; }
}

/// <summary>
/// Redacts secret-shaped substrings before they reach the model or logs.
/// </summary>
public interface IAiSecretRedactionService
{
    string Redact(string input);
    bool IsSecretFile(string filePath);
}

/// <summary>
/// Persists and retrieves agent run state. The infrastructure layer
/// provides the EF Core-backed implementation.
/// </summary>
public interface IAiRunRepository
{
    Task<AiAgentRun> CreateAsync(AiAgentRun run, CancellationToken cancellationToken);
    Task<AiAgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken);
    Task UpdateAsync(AiAgentRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiAgentRunSummary>> ListForProjectAsync(Guid projectId, int skip, int take, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the active prompt version so the orchestrator can record which
/// prompt generated the run.
/// </summary>
public interface IAiPromptVersionService
{
    string CurrentVersion { get; }
}

/// <summary>
/// Coordinates an entire agent run, including pause/resume around approval.
/// </summary>
public interface IAiAgentOrchestrator
{
    Task<AiAgentRunSummary> StartAsync(AiAgentStartRequest request, CancellationToken cancellationToken);
    Task<AiAgentRunSummary> ResumeAsync(Guid runId, CancellationToken cancellationToken);
    Task CancelAsync(Guid runId, CancellationToken cancellationToken);
    Task<AiPatchPreview> PreviewPatchAsync(Guid runId, Guid patchId, CancellationToken cancellationToken);
    Task<AiPatchApplyResult> ApplyPatchAsync(Guid runId, Guid patchId, CancellationToken cancellationToken);
    Task<AiPatchApplyResult> RollbackPatchAsync(Guid runId, Guid patchId, CancellationToken cancellationToken);
    Task<AiApprovalRequestSummary> ApproveOnceAsync(Guid runId, Guid approvalId, CancellationToken cancellationToken);
    Task<AiApprovalRequestSummary> ApproveLowRiskForRunAsync(Guid runId, Guid approvalId, CancellationToken cancellationToken);
    Task<AiApprovalRequestSummary> RejectAsync(Guid runId, Guid approvalId, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the structured repository context the planner and reviewer use.
/// Mirrors the existing <see cref="Coding.Application.Features.AiAssistant.AiRepositoryContext"/>
/// but is owned by the agent feature so changes stay self-contained.
/// </summary>
public sealed record AiAgentRepositoryContext(
    string SystemInstructions,
    string UserInstructions,
    string RepositoryContent,
    IReadOnlyList<Guid> IncludedFileIds);

public interface IAiAgentContextBuilder
{
    Task<AiAgentRepositoryContext> BuildAsync(AiAgentRun run, CancellationToken cancellationToken);
}

/// <summary>
/// Envelope passed from the orchestrator to the centralized execution
/// pipeline. The pipeline resolves the tool, validates the request, and
/// returns a structured outcome that the orchestrator can persist.
/// </summary>
public sealed record AiToolDispatchRequest(
    AiAgentRun Run,
    AiToolCall ToolCall,
    JsonElement Arguments);

/// <summary>
/// Result of running a tool call through the centralized pipeline. The
/// orchestrator inspects <see cref="Outcome"/> to decide whether to persist
/// a result, pause for approval, or fail the run.
/// </summary>
public sealed record AiToolDispatchResult(
    AiToolDispatchOutcome Outcome,
    string? Summary,
    string? ResultJson,
    string? ErrorMessage,
    AiApprovalStatus? ApprovalStatus,
    DateTime? ApprovalExpiresAt,
    Guid? ApprovalRequestId)
{
    public static AiToolDispatchResult Executed(string summary, string? resultJson) =>
        new(AiToolDispatchOutcome.Executed, summary, resultJson, null, null, null, null);

    public static AiToolDispatchResult ApprovalRequired(Guid approvalId, DateTime expiresAt) =>
        new(AiToolDispatchOutcome.ApprovalRequired, null, null, null,
            AiApprovalStatus.Pending, expiresAt, approvalId);

    public static AiToolDispatchResult Rejected(string reason) =>
        new(AiToolDispatchOutcome.Rejected, null, null, reason, null, null, null);

    public static AiToolDispatchResult Failed(string error) =>
        new(AiToolDispatchOutcome.Failed, null, null, error, null, null, null);

    public static AiToolDispatchResult Blocked(string reason) =>
        new(AiToolDispatchOutcome.Blocked, null, null, reason, null, null, null);

    public static AiToolDispatchResult Duplicate(string summary) =>
        new(AiToolDispatchOutcome.Duplicate, summary, null, "The tool call has already executed.", null, null, null);
}

public enum AiToolDispatchOutcome
{
    Executed = 0,
    ApprovalRequired = 1,
    Rejected = 2,
    Failed = 3,
    Blocked = 4,
    Duplicate = 5
}

/// <summary>
/// Centralized execution pipeline used by the orchestrator. Performs tool
/// resolution, argument validation, authorization, risk classification,
/// approval policy, and idempotency in a single, auditable code path.
/// </summary>
public interface IAiToolExecutionService
{
    Task<AiToolDispatchResult> DispatchAsync(AiToolDispatchRequest request, CancellationToken cancellationToken);
}

public sealed record AiApprovalDetails(
    Guid Id, Guid AgentRunId, Guid ProjectId, string ProjectName, string Goal,
    Guid ToolCallId, string ToolName, AiToolRiskLevel RiskLevel,
    AiApprovalStatus Status, string ArgumentsJson, DateTime CreatedAt,
    DateTime ExpiresAt, DateTime? RespondedAt, string? ResultSummary, string? ErrorMessage);

public interface IAiApprovalService
{
    Task<IReadOnlyList<AiApprovalDetails>> ListAsync(Guid? projectId, CancellationToken cancellationToken);
    Task<AiApprovalDetails> GetAsync(Guid approvalId, CancellationToken cancellationToken);
    Task<AiApprovalDetails> ApproveAsync(Guid approvalId, CancellationToken cancellationToken);
    Task<AiApprovalDetails> RejectAsync(Guid approvalId, CancellationToken cancellationToken);
}
