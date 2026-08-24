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
/// Reads a single file's content. Rejects secret files, oversize files, and
/// non-text mime types. Wraps the response as untrusted content.
/// </summary>
public sealed class ReadFileTool(AppDbContext db, IAiSecretRedactionService redaction) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "read_file",
        Description: "Reads a single file's content from the project. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(ReadFileInput));

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

        if (Encoding.UTF8.GetByteCount(content) > AiPathGuard.MaxReadBytes)
            return new AiReadToolGuard.AiTextResult("File too large to return.", "{\"tooLarge\":true}");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        var wrapped = AiReadToolGuard.WrapUntrustedContent(path, null, null, hash, content);
        var json = JsonSerializer.Serialize(new { path, hash, byteLength = Encoding.UTF8.GetByteCount(content), content });
        return new AiReadToolGuard.AiTextResult($"Read {path}", json);
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

    private static ReadFileInput ParseInput(JsonElement raw)
    {
        var fileName = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("fileName", out var fn)
            ? fn.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName is required.");
        AiPathGuard.NormalizeProjectRelativePath(fileName);
        return new ReadFileInput(fileName);
    }
}

public sealed record ReadFileInput(string FileName);