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
/// Case-insensitive substring search across the project's file contents.
/// Read-only. Capped result count.
/// </summary>
public sealed class SearchCodeTool(AppDbContext db, IAiSecretRedactionService redaction) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "search_code",
        Description: "Case-insensitive substring search across project file contents. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(SearchCodeInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        var input = ParseInput(arguments);
        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Length < 2)
            return AiReadToolGuard.Failure("Query must be at least 2 characters.");
        if (input.Query.Length > 100)
            return AiReadToolGuard.Failure("Query is too long (max 100 chars).");

        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);
        var files = await db.WorkspaceNodes.AsNoTracking()
            .Where(n => n.ProjectId == run.ProjectId && !n.IsDeleted && n.NodeType == WorkspaceNodeType.File)
            .Join(db.FileContents, n => n.ID, c => c.NodeId, (n, c) => new { n, c })
            .Where(x => x.c.Content != null && EF.Functions.ILike(x.c.Content, "%" + input.Query + "%"))
            .Take(AiPathGuard.MaxSearchResults)
            .ToListAsync(cancellationToken);

        var matches = new List<object>();
        foreach (var entry in files)
        {
            var path = await NodePathAsync(entry.n, cancellationToken);
            if (redaction.IsSecretFile(path)) continue;
            var lines = (entry.c.Content ?? string.Empty).Split('\n');
            var hits = new List<int>();
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains(input.Query, StringComparison.OrdinalIgnoreCase))
                    hits.Add(i + 1);
            if (hits.Count > 0)
                matches.Add(new { path, lines = hits.Take(20).ToArray() });
        }
        var json = JsonSerializer.Serialize(new { query = input.Query, matches });
        return new AiReadToolGuard.AiTextResult($"{matches.Count} files match", json);
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

    private static SearchCodeInput ParseInput(JsonElement raw)
    {
        var query = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("query", out var q)
            ? q.GetString()
            : null;
        var glob = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("pathGlob", out var g)
            ? g.GetString()
            : null;
        if (glob is not null) AiPathGuard.NormalizeProjectRelativePath(glob);
        return new SearchCodeInput(query ?? string.Empty, glob);
    }
}

public sealed record SearchCodeInput(string Query, string? PathGlob = null);