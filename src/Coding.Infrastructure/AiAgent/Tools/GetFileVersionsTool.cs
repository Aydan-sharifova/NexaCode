using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns the version history for a file. Read-only.
/// </summary>
public sealed class GetFileVersionsTool(AppDbContext db) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "get_file_versions",
        Description: "Returns the version history for a file. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(GetFileVersionsInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        var input = ParseInput(arguments);
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);
        var node = await db.WorkspaceNodes.AsNoTracking()
            .Where(n => n.ProjectId == run.ProjectId && !n.IsDeleted && n.NodeType == WorkspaceNodeType.File)
            .FirstOrDefaultAsync(n => n.Name == input.FileName, cancellationToken)
            ?? throw new NotFoundException("File not found.");

        var versions = await db.FileVersions.AsNoTracking()
            .Where(v => v.NodeId == node.ID)
            .OrderByDescending(v => v.VersionNumber)
            .Take(50)
            .Select(v => new
            {
                versionNumber = v.VersionNumber,
                contentHash = v.ContentHash,
                createdAt = v.CreatAt,
                createdBy = v.CreatedBy.UserName
            })
            .ToListAsync(cancellationToken);
        var json = JsonSerializer.Serialize(new { path = input.FileName, versions });
        return new AiReadToolGuard.AiTextResult($"{versions.Count} versions", json);
    }

    private static GetFileVersionsInput ParseInput(JsonElement raw)
    {
        var name = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("fileName", out var fn)
            ? fn.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("fileName is required.");
        AiPathGuard.NormalizeProjectRelativePath(name);
        return new GetFileVersionsInput(name);
    }
}

public sealed record GetFileVersionsInput(string FileName);