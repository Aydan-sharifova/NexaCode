using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns the project tree as a flat list of paths. Read-only.
/// </summary>
public sealed class GetProjectTreeTool(AppDbContext db, IAiSecretRedactionService redaction) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "get_project_tree",
        Description: "Returns the project tree as a list of file and folder paths. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(GetProjectTreeInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        var input = ParseInput(arguments);
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);
        var nodes = await db.WorkspaceNodes.AsNoTracking()
            .Where(n => n.ProjectId == run.ProjectId && !n.IsDeleted)
            .OrderBy(n => n.NodeType)
            .ThenBy(n => n.Name)
            .ToListAsync(cancellationToken);

        var paths = new List<string>();
        foreach (var n in nodes)
        {
            var p = await NodePathAsync(n, cancellationToken);
            if (!redaction.IsSecretFile(p)) paths.Add(p);
        }
        var json = JsonSerializer.Serialize(new { projectId = run.ProjectId, paths });
        return new AiReadToolGuard.AiTextResult($"{paths.Count} project paths", json);
    }

    private async Task<string> NodePathAsync(WorkspaceNode node, CancellationToken ct)
    {
        var parts = new Stack<string>();
        var current = node;
        var seen = new HashSet<Guid>();
        while (true)
        {
            if (!seen.Add(current.ID)) return "/" + string.Join('/', parts);
            parts.Push(current.Name);
            if (current.ParentId is null) break;
            current = await db.WorkspaceNodes.AsNoTracking().SingleAsync(n => n.ID == current.ParentId, ct);
        }
        return "/" + string.Join('/', parts);
    }

    private static GetProjectTreeInput ParseInput(JsonElement raw)
    {
        int? maxDepth = null;
        if (raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("maxDepth", out var md) && md.TryGetInt32(out var depth))
            maxDepth = depth;
        return new GetProjectTreeInput(maxDepth);
    }
}

public sealed record GetProjectTreeInput(int? MaxDepth = null);