using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns bounded, redacted evidence from a project execution. Successful
/// runs intentionally retain metadata only; failed runs may expose their
/// correlated debugging incident to the project's authorized agent.
/// </summary>
public sealed class GetExecutionResultTool(AppDbContext db, IAiSecretRedactionService redaction) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "get_execution_result",
        Description: "Returns the structured result of a previous sandbox execution. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(GetExecutionResultInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);
        var input = ParseInput(arguments);
        if (!Guid.TryParse(input.ExecutionId, out var executionId))
            throw new ArgumentException("executionId must be a valid identifier.");
        var execution = await db.DebuggingExecutionObservations.AsNoTracking()
            .Include(item => item.Incident)
            .SingleOrDefaultAsync(item => item.ID == executionId && item.ProjectId == run.ProjectId, cancellationToken);
        if (execution is null)
            return new AiReadToolGuard.AiTextResult("Execution result not found.", "{\"found\":false}");
        var incident = execution.Incident;
        var json = JsonSerializer.Serialize(new
        {
            found = true,
            executionId = execution.ID,
            execution.Language,
            kind = execution.Kind.ToString(),
            execution.Succeeded,
            execution.ExitCode,
            execution.TimedOut,
            execution.DurationMs,
            execution.ExecutedAt,
            execution.WorkspaceNodeId,
            outputAvailable = incident is not null,
            incident = incident is null ? null : new
            {
                incidentId = incident.ID,
                status = incident.Status.ToString(),
                incident.ErrorSummary,
                stackTrace = Limit(incident.StackTrace, 12_000),
                stdout = Limit(incident.Stdout, 12_000),
                stderr = Limit(incident.Stderr, 12_000)
            },
            note = incident is null
                ? "Successful execution output is not retained; only bounded metadata is available."
                : "Failure output is provided from the persisted debugging incident."
        });
        return new AiReadToolGuard.AiTextResult(incident is null ? "Execution metadata retrieved" : "Execution failure evidence retrieved", redaction.Redact(json));
    }

    private static GetExecutionResultInput ParseInput(JsonElement raw)
    {
        var id = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("executionId", out var v)
            ? v.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("executionId is required.");
        return new GetExecutionResultInput(id);
    }

    private static string? Limit(string? value, int maximum) => string.IsNullOrEmpty(value)
        ? value
        : value.Length <= maximum ? value : value[..maximum] + "\n… output truncated";
}

public sealed record GetExecutionResultInput(string ExecutionId);
