using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Reads a contiguous line range from a file. Read-only. Rejects oversize
/// ranges and secret files.
/// </summary>
public sealed class ReadFileRangeTool(AppDbContext db, IAiSecretRedactionService redaction) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "read_file_range",
        Description: "Reads a contiguous line range from a file. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(ReadFileRangeInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        var input = ParseInput(arguments);
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);

        var node = await db.WorkspaceNodes.AsNoTracking()
            .Where(n => n.ProjectId == run.ProjectId && !n.IsDeleted && n.NodeType == WorkspaceNodeType.File)
            .FirstOrDefaultAsync(n => n.Name == input.FileName, cancellationToken)
            ?? throw new NotFoundException($"File '{input.FileName}' was not found in this project.");

        var path = await NodePathAsync(node, cancellationToken);
        if (redaction.IsSecretFile(path))
            return new AiReadToolGuard.AiTextResult("Sensitive file blocked.", "{\"blocked\":true}");

        var content = await db.FileContents.AsNoTracking()
            .Where(c => c.NodeId == node.ID)
            .Select(c => c.Content)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        var lines = content.Split('\n');
        if (input.StartLine < 1 || input.EndLine < input.StartLine)
            throw new ArgumentException("Invalid line range.");
        if (input.EndLine - input.StartLine + 1 > AiPathGuard.MaxLineRange)
            return new AiReadToolGuard.AiTextResult("Range too large.", "{\"tooLarge\":true}");

        var startIdx = Math.Min(input.StartLine - 1, lines.Length);
        var endIdx = Math.Min(input.EndLine, lines.Length);
        var slice = string.Join('\n', lines[startIdx..endIdx]);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slice)));
        var wrapped = AiReadToolGuard.WrapUntrustedContent(path, input.StartLine, input.EndLine, hash, slice);
        var json = JsonSerializer.Serialize(new { path, startLine = input.StartLine, endLine = input.EndLine, hash, content = slice });
        return new AiReadToolGuard.AiTextResult($"Read {path} L{input.StartLine}-{input.EndLine}", json);
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

    private static ReadFileRangeInput ParseInput(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Object arguments expected.");

        var fileName = raw.TryGetProperty("fileName", out var fn) ? fn.GetString() : null;
        var start = raw.TryGetProperty("startLine", out var s) && s.TryGetInt32(out var sLine) ? sLine : 0;
        var end = raw.TryGetProperty("endLine", out var e) && e.TryGetInt32(out var eLine) ? eLine : 0;

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName is required.");
        AiPathGuard.NormalizeProjectRelativePath(fileName);
        if (start <= 0 || end <= 0 || end < start)
            throw new ArgumentException("startLine and endLine must be positive integers with end >= start.");
        return new ReadFileRangeInput(fileName, start, end);
    }
}

public sealed record ReadFileRangeInput(string FileName, int StartLine, int EndLine);