using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Application.Features.Activities;
using Coding.Data;
using Coding.Enums;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coding.Infrastructure.AiAgent;

/// <summary>
/// Centralized execution pipeline used by the orchestrator. Performs:
///   1. Argument validation
///   2. Tool resolution
///   3. Authorization
///   4. Risk classification
///   5. Approval policy
///   6. Idempotency check
///   7. Execution
///   8. Safe persistence of result
///   9. Activity log
/// </summary>
public sealed class AiToolExecutionService : IAiToolExecutionService
{
    private readonly AppDbContext _db;
    private readonly IAiToolRegistry _registry;
    private readonly IAiToolAuthorizationService _authorization;
    private readonly IAiToolApprovalPolicy _approval;
    private readonly IActivityLogger _activity;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _approvalLifetime;
    private readonly IServiceProvider _services;
    private readonly IAiSecretRedactionService _redaction;

    public AiToolExecutionService(
        AppDbContext db,
        IAiToolRegistry registry,
        IAiToolAuthorizationService authorization,
        IAiToolApprovalPolicy approval,
        IActivityLogger activity,
        TimeProvider clock,
        IServiceProvider services,
        IAiSecretRedactionService redaction,
        TimeSpan? approvalLifetime = null)
    {
        _db = db;
        _registry = registry;
        _authorization = authorization;
        _approval = approval;
        _activity = activity;
        _clock = clock;
        _services = services;
        _redaction = redaction;
        _approvalLifetime = approvalLifetime ?? TimeSpan.FromMinutes(15);
    }

    public async Task<AiToolDispatchResult> DispatchAsync(AiToolDispatchRequest request, CancellationToken cancellationToken)
    {
        var run = request.Run;
        var call = request.ToolCall;

        // 1. Reject malformed arguments early.
        if (call.ArgumentsJson is null)
            return AiToolDispatchResult.Failed("Tool call has no arguments.");

        // 2. Resolve tool.
        if (!_registry.TryGet(call.ToolName, out var tool))
            return AiToolDispatchResult.Failed($"Unknown tool '{call.ToolName}'.");

        AiToolDescriptor descriptor;
        try
        {
            descriptor = _registry.Describe(call.ToolName);
        }
        catch (UnknownAiToolException)
        {
            return AiToolDispatchResult.Failed($"Unknown tool '{call.ToolName}'.");
        }

        // 3. Authorization gate (membership, role, mode, environment).
        var auth = await _authorization.AuthorizeAsync(call, run, cancellationToken);
        if (!auth.IsAllowed)
            return AiToolDispatchResult.Blocked(auth.Reason ?? "Authorization denied.");

        // 4. Risk classification (centralized, never trust the model's value).
        var risk = AiRiskPolicy.Classify(call.ToolName, run);
        if (!AiRiskPolicy.ModeAllowsRisk(run.Mode, risk))
            return AiToolDispatchResult.Blocked(
                $"Mode '{run.Mode}' cannot execute tool with risk level '{risk}'.");
        if (risk == AiToolRiskLevel.Critical)
            return AiToolDispatchResult.Blocked("Critical risk tools are blocked.");

        // 5. Idempotency check.
        var idempotencyKey = ComputeIdempotencyKey(run.ID, call.ToolName, call.ArgumentsJson);
        var prior = await _db.AiToolCalls.AsNoTracking()
            .Where(c => c.IdempotencyKey == idempotencyKey && !c.IsDeleted)
            .OrderByDescending(c => c.ExecutedAt ?? c.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (prior is not null && prior.ExecutedAt is not null)
            return AiToolDispatchResult.Duplicate(prior.ResultSummary ?? "Already executed.");

        // 6. Approval policy.
        if (_approval.RequiresApproval(descriptor) ||
            (risk == AiToolRiskLevel.Low && !_approval.CanAutoApproveLowRisk(run, descriptor)))
        {
            var argsHash = AiSecretRedactionService.HashArguments(request.Arguments);
            var approval = new AiApprovalRequest
            {
                ID = Guid.NewGuid(),
                AgentRunId = run.ID,
                ToolCallId = call.ID,
                UserId = run.UserId,
                Status = AiApprovalStatus.Pending,
                ArgumentsHash = argsHash,
                ExpiresAt = _clock.GetUtcNow().UtcDateTime.Add(_approvalLifetime),
                CreatAt = _clock.GetUtcNow().UtcDateTime
            };
            _db.AiApprovalRequests.Add(approval);
            call.ApprovalStatus = AiApprovalStatus.Pending;
            call.IdempotencyKey = idempotencyKey;
            call.RiskLevel = risk;
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(run, call.ToolName, risk, AiApprovalStatus.Pending, "approval_requested", cancellationToken);
            return AiToolDispatchResult.ApprovalRequired(approval.ID, approval.ExpiresAt);
        }

        // 7. Execute.
        try
        {
            var result = await tool.ExecuteAsync(request.Arguments, run, cancellationToken);
            var now = _clock.GetUtcNow().UtcDateTime;
            call.ResultSummary = _redaction.Redact(result.Summary);
            call.ResultJson = result.Json is null ? null : _redaction.Redact(result.Json);
            call.ExecutedAt = now;
            call.ApprovalStatus = AiApprovalStatus.NotRequired;
            call.IdempotencyKey = idempotencyKey;
            call.RiskLevel = risk;
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(run, call.ToolName, risk, AiApprovalStatus.NotRequired, "executed", cancellationToken);
            return AiToolDispatchResult.Executed(call.ResultSummary, call.ResultJson);
        }
        catch (UnknownAiToolException ex)
        {
            return AiToolDispatchResult.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            call.ErrorMessage = _redaction.Redact(ex.Message);
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(run, call.ToolName, risk, call.ApprovalStatus, "failed", cancellationToken);
            return AiToolDispatchResult.Failed(call.ErrorMessage);
        }
    }

    private static string ComputeIdempotencyKey(Guid runId, string toolName, string argumentsJson)
    {
        // Normalize JSON before hashing so semantically identical arguments
        // produce the same key even when the model emits different key order.
        JsonElement parsed;
        try
        {
            parsed = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson).RootElement;
        }
        catch (JsonException)
        {
            parsed = JsonDocument.Parse("{}").RootElement;
        }
        var canonical = AiSecretRedactionService.Canonicalize(parsed);
        var combined = $"{runId:N}|{toolName}|{canonical}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private Task LogAsync(AiAgentRun run, string toolName, AiToolRiskLevel risk, AiApprovalStatus approvalStatus, string outcome, CancellationToken cancellationToken)
    {
        return _activity.LogAsync(new ActivityWrite(
            UserId: run.UserId,
            ProjectId: run.ProjectId,
            ActionType: $"ai_agent.tool.{outcome}",
            EntityType: nameof(AiToolCall),
            EntityId: null,
            Description: $"Tool '{toolName}' (risk={risk}, approval={approvalStatus}) → {outcome}",
            Metadata: new Dictionary<string, object?>
            {
                ["agentRunId"] = run.ID,
                ["toolName"] = toolName,
                ["riskLevel"] = risk.ToString(),
                ["approvalStatus"] = approvalStatus.ToString(),
                ["mode"] = run.Mode.ToString()
            }), cancellationToken);
    }
}
