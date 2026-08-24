using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
using Coding.Models;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns the result of a previous sandbox execution. The sandbox is not
/// implemented in Phase 2; this tool returns a structured "unavailable"
/// response so the orchestrator can plan against it during the sandbox phase.
/// </summary>
public sealed class GetExecutionResultTool(AppDbContext db) : IAiTool
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
        var json = JsonSerializer.Serialize(new
        {
            executionId = input.ExecutionId,
            available = false,
            reason = "Sandbox execution is not available in this version."
        });
        return new AiReadToolGuard.AiTextResult("Execution result unavailable", json);
    }

    private static GetExecutionResultInput ParseInput(JsonElement raw)
    {
        var id = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("executionId", out var v)
            ? v.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("executionId is required.");
        return new GetExecutionResultInput(id);
    }
}

public sealed record GetExecutionResultInput(string ExecutionId);