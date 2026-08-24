using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns recent activity log entries for the project. Read-only.
/// </summary>
public sealed class GetRecentActivityTool(AppDbContext db, IAiSecretRedactionService redaction) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "get_recent_activity",
        Description: "Returns recent activity log entries for the project. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(GetRecentActivityInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);
        var input = ParseInput(arguments);
        var take = Math.Clamp(input.Limit ?? 25, 1, 100);

        var entries = await db.ActivityLogs.AsNoTracking()
            .Where(a => a.ProjectId == run.ProjectId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                userName = a.User != null ? a.User.UserName : null,
                a.ActionType,
                a.EntityType,
                a.EntityId,
                a.Description,
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var json = redaction.Redact(JsonSerializer.Serialize(new { entries }));
        return new AiReadToolGuard.AiTextResult($"{entries.Count} recent activity entries", json);
    }

    private static GetRecentActivityInput ParseInput(JsonElement raw)
    {
        var limit = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("limit", out var v) && v.TryGetInt32(out var n)
            ? n : (int?)null;
        return new GetRecentActivityInput(limit);
    }
}

public sealed record GetRecentActivityInput(int? Limit);
