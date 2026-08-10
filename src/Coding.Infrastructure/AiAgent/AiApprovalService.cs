using System.Data;
using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent;

public sealed class AiApprovalService(
    AppDbContext db, ICurrentUser user, IAiToolRegistry registry,
    IAiToolAuthorizationService authorization, IActivityLogger activity,
    IAiSecretRedactionService redaction, TimeProvider clock) : IAiApprovalService
{
    public async Task<IReadOnlyList<AiApprovalDetails>> ListAsync(Guid? projectId, CancellationToken ct)
    {
        var query = db.AiApprovalRequests.AsNoTracking().Include(x => x.AgentRun).ThenInclude(x => x.Project).Include(x => x.ToolCall)
            .Where(x => x.UserId == user.UserId && x.AgentRun.Project.Members.Any(m => m.UserId == user.UserId));
        if (projectId.HasValue) query = query.Where(x => x.AgentRun.ProjectId == projectId.Value);
        var items = await query.OrderBy(x => x.Status == AiApprovalStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatAt).Take(100).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<AiApprovalDetails> GetAsync(Guid approvalId, CancellationToken ct) => Map(await LoadAuthorizedAsync(approvalId, ct));

    public async Task<AiApprovalDetails> ApproveAsync(Guid approvalId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var approval = await LoadAuthorizedAsync(approvalId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (approval.Status != AiApprovalStatus.Pending) throw new ConflictException("This approval has already been resolved.");
        if (approval.ExpiresAt <= now) { approval.Status = AiApprovalStatus.Expired; approval.RespondedAt = now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); throw new ConflictException("This approval has expired."); }
        if (approval.ToolCall.ExecutedAt.HasValue) throw new ConflictException("This tool call has already executed.");
        using var arguments = JsonDocument.Parse(approval.ToolCall.ArgumentsJson);
        var currentHash = AiSecretRedactionService.HashArguments(arguments.RootElement);
        if (!string.Equals(currentHash, approval.ArgumentsHash, StringComparison.Ordinal)) throw new ConflictException("The tool arguments changed after approval was requested.");
        var decision = await authorization.AuthorizeAsync(approval.ToolCall, approval.AgentRun, ct);
        if (!decision.IsAllowed) throw new ForbiddenException(decision.Reason ?? "Tool execution is no longer authorized.");
        if (!registry.TryGet(approval.ToolCall.ToolName, out var tool)) throw new ConflictException("The requested tool is no longer registered.");
        var risk = AiRiskPolicy.Classify(approval.ToolCall.ToolName, approval.AgentRun);
        if (risk == AiToolRiskLevel.Critical || !AiRiskPolicy.ModeAllowsRisk(approval.AgentRun.Mode, risk)) throw new ForbiddenException("The requested tool risk is not allowed for this run.");
        approval.Status = AiApprovalStatus.ApprovedOnce;
        approval.RespondedAt = now;
        approval.ToolCall.ApprovalStatus = AiApprovalStatus.ApprovedOnce;
        approval.ToolCall.ApprovedAt = now;
        approval.AgentRun.Status = AiAgentStatus.Executing;
        await db.SaveChangesAsync(ct);
        try
        {
            var result = await tool.ExecuteAsync(arguments.RootElement, approval.AgentRun, ct);
            approval.ToolCall.ResultSummary = result.Summary;
            approval.ToolCall.ResultJson = result.Json;
            approval.ToolCall.ExecutedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await LogAsync(approval, "approved_and_executed", ct);
            return Map(approval);
        }
        catch (Exception exception)
        {
            approval.ToolCall.ErrorMessage = redaction.Redact(exception.Message);
            approval.AgentRun.Status = AiAgentStatus.Failed;
            approval.AgentRun.ErrorMessage = "Approved tool execution failed.";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await LogAsync(approval, "execution_failed", ct);
            throw new ConflictException("The approved tool failed to execute.");
        }
    }

    public async Task<AiApprovalDetails> RejectAsync(Guid approvalId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var approval = await LoadAuthorizedAsync(approvalId, ct);
        if (approval.Status != AiApprovalStatus.Pending) throw new ConflictException("This approval has already been resolved.");
        approval.Status = AiApprovalStatus.Rejected; approval.RespondedAt = clock.GetUtcNow().UtcDateTime;
        approval.ToolCall.ApprovalStatus = AiApprovalStatus.Rejected;
        approval.AgentRun.Status = AiAgentStatus.Cancelled; approval.AgentRun.CancelledAt = approval.RespondedAt;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await LogAsync(approval, "rejected", ct);
        return Map(approval);
    }

    private async Task<AiApprovalRequest> LoadAuthorizedAsync(Guid id, CancellationToken ct)
    {
        var approval = await db.AiApprovalRequests.Include(x => x.AgentRun).ThenInclude(x => x.Project).Include(x => x.ToolCall).SingleOrDefaultAsync(x => x.ID == id, ct) ?? throw new NotFoundException("AI approval not found.");
        if (approval.UserId != user.UserId) throw new ForbiddenException("This approval belongs to another user.");
        await ProjectAccess.RequireMemberAsync(db, approval.AgentRun.ProjectId, user.UserId, ct);
        return approval;
    }

    private AiApprovalDetails Map(AiApprovalRequest x) => new(x.ID,x.AgentRunId,x.AgentRun.ProjectId,x.AgentRun.Project.Name,x.AgentRun.Goal,x.ToolCallId,x.ToolCall.ToolName,x.ToolCall.RiskLevel,x.Status,redaction.Redact(x.ToolCall.ArgumentsJson),x.CreatAt,x.ExpiresAt,x.RespondedAt,x.ToolCall.ResultSummary,x.ToolCall.ErrorMessage);
    private Task LogAsync(AiApprovalRequest x,string outcome,CancellationToken ct)=>activity.LogAsync(new ActivityWrite(user.UserId,x.AgentRun.ProjectId,$"ai_approval.{outcome}",nameof(AiApprovalRequest),x.ID,$"AI tool '{x.ToolCall.ToolName}' {outcome}.",new Dictionary<string,object?>{{"runId",x.AgentRunId},{"toolCallId",x.ToolCallId},{"risk",x.ToolCall.RiskLevel.ToString()}}),ct);
}
