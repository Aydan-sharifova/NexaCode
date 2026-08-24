using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.FileExplorer;
using Coding.Application.Features.ScreenshotToCode;
using Coding.Application.Security;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.ScreenshotToCode;

public sealed class ScreenshotToCodeService(AppDbContext db, ICurrentUser currentUser, IAiProvider provider, ISender sender) : IScreenshotToCodeService
{
    private static readonly HashSet<string> MediaTypes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp" };
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private sealed record TargetSnapshot(string Path, Guid? NodeId, string? Content, string? Token);

    public async Task<ScreenshotGenerationDto> GenerateAsync(CreateScreenshotGeneration request, CancellationToken ct)
    {
        await ProjectAccess.RequireWorkspaceWriteAsync(db, request.ProjectId, currentUser.UserId, ct);
        if (!MediaTypes.Contains(request.MediaType)) throw new ArgumentException("Only PNG, JPEG, and WebP design images are supported.");
        if (request.Image.Length is 0 or > MaximumImageBytes) throw new ArgumentException($"The image must be between 1 byte and {MaximumImageBytes / 1024 / 1024} MB.");
        if(!ImageUploadPolicy.HasValidSignature(request.Image,request.MediaType))throw new ArgumentException("The uploaded image content does not match its declared media type.");
        var prompt = request.Prompt.Trim();
        if (prompt.Length is < 10 or > 2000) throw new ArgumentException("Describe the intended page in 10 to 2,000 characters.");

        var snapshots = await LoadTargets(request.ProjectId, ct);
        var aiRequest = new AiRequest(
            "You are a screenshot-to-code system. Analyze only visible evidence. Return exactly the marked sections requested. Never claim hidden interactions as observed. Produce accessible responsive React TypeScript without external assets or remote URLs.",
            "Analyze layout, typography, spacing, components, responsive structure, colors, and likely interactions. Build a faithful production-quality page. Return exactly: [[[ANALYSIS]]] then concise evidence; [[[APP_TSX]]] then complete React TypeScript component; [[[STYLES_CSS]]] then complete plain CSS; [[[PREVIEW_HTML]]] then standalone HTML with embedded CSS that visually previews the same page. Do not use markdown fences. User intent: " + prompt,
            snapshots.Count == 0 ? "No target files currently exist." : string.Join("\n\n", snapshots.Select(x => $"Existing {x.Path}:\n{x.Content}")),
            "typescript", AiAssistantAction.GenerateCode, [], [new(request.FileName, request.MediaType, Convert.ToBase64String(request.Image))], 4096);

        var output = new StringBuilder();
        await foreach (var chunk in provider.StreamAsync(aiRequest, ct)) output.Append(chunk.Content);
        var raw = output.ToString();
        var analysis = ScreenshotCodePolicy.ExtractSection(raw, "ANALYSIS");
        var app = ScreenshotCodePolicy.ExtractSection(raw, "APP_TSX");
        var css = ScreenshotCodePolicy.ExtractSection(raw, "STYLES_CSS");
        var preview = ScreenshotCodePolicy.ExtractSection(raw, "PREVIEW_HTML");
        ScreenshotCodePolicy.ValidateGenerated(app, css, preview);
        preview = ScreenshotCodePolicy.SecurePreview(preview);
        var now = DateTime.UtcNow;
        var entity = new ScreenshotCodeGeneration
        {
            ID = Guid.NewGuid(), ProjectId = request.ProjectId, UserId = currentUser.UserId, Prompt = prompt,
            ImageFileName = Path.GetFileName(request.FileName)[..Math.Min(Path.GetFileName(request.FileName).Length, 255)],
            ImageMediaType = request.MediaType, ImageHash = Convert.ToHexString(SHA256.HashData(request.Image)).ToLowerInvariant(),
            Status = ScreenshotGenerationStatus.Draft, Analysis = Limit(analysis, 8000), AppTsx = app, StylesCss = css,
            PreviewHtml = preview, TargetSnapshotsJson = JsonSerializer.Serialize(snapshots), ModelProvider = provider.ProviderName,
            ModelName = provider.Model, GeneratedAt = now, CreatAt = now
        };
        db.ScreenshotCodeGenerations.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity, snapshots);
    }

    public async Task<IReadOnlyList<ScreenshotGenerationDto>> ListAsync(Guid projectId, int take, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct);
        var rows = await db.ScreenshotCodeGenerations.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.GeneratedAt).Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        return rows.Select(x => Map(x, ReadSnapshots(x))).ToList();
    }

    public async Task<ScreenshotGenerationDto> GetAsync(Guid projectId, Guid generationId, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct);
        var entity = await Find(projectId, generationId, ct);
        return Map(entity, ReadSnapshots(entity));
    }

    public async Task<ScreenshotGenerationDto> ApplyAsync(Guid projectId, Guid generationId, bool confirm, CancellationToken ct)
    {
        if (!confirm) throw new ArgumentException("Explicit confirmation is required before generated code is written.");
        await ProjectAccess.RequireWorkspaceWriteAsync(db, projectId, currentUser.UserId, ct);
        var entity = await Find(projectId, generationId, ct);
        if (entity.Status != ScreenshotGenerationStatus.Draft) throw new ConflictException("Only an unapplied screenshot draft can be applied.");
        var snapshots = ReadSnapshots(entity);
        foreach (var target in snapshots.Where(x => x.NodeId.HasValue))
        {
            var current = await db.FileContents.AsNoTracking().SingleOrDefaultAsync(x => x.NodeId == target.NodeId, ct);
            if (current is null || current.ConcurrencyToken != target.Token) throw new ConflictException($"{target.Path} changed after generation. Generate a new draft before applying.");
        }
        foreach (var (path, content) in new[] { ("App.tsx", entity.AppTsx), ("styles.css", entity.StylesCss) })
        {
            var target = snapshots.Single(x => x.Path == path);
            if (target.NodeId.HasValue) await sender.Send(new SaveFileContentCommand(target.NodeId.Value, content, target.Token!), ct);
            else await sender.Send(new CreateFileCommand(projectId, null, path, content), ct);
        }
        entity.Status = ScreenshotGenerationStatus.Applied; entity.AppliedAt = entity.UpdateAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(entity, snapshots);
    }

    private async Task<List<TargetSnapshot>> LoadTargets(Guid projectId, CancellationToken ct)
    {
        var rows = await db.WorkspaceNodes.AsNoTracking().Where(x => x.ProjectId == projectId && x.ParentId == null &&
            x.NodeType == WorkspaceNodeType.File && (x.Name == "App.tsx" || x.Name == "styles.css"))
            .Select(x => new { x.Name, x.ID, x.FileContent!.Content, x.FileContent.ConcurrencyToken }).ToListAsync(ct);
        return new[] { "App.tsx", "styles.css" }.Select(path => { var row = rows.SingleOrDefault(x => x.Name == path); return new TargetSnapshot(path, row?.ID, row?.Content, row?.ConcurrencyToken); }).ToList();
    }

    private async Task<ScreenshotCodeGeneration> Find(Guid projectId, Guid id, CancellationToken ct) =>
        await db.ScreenshotCodeGenerations.SingleOrDefaultAsync(x => x.ID == id && x.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Screenshot generation not found.");
    private static List<TargetSnapshot> ReadSnapshots(ScreenshotCodeGeneration x) => JsonSerializer.Deserialize<List<TargetSnapshot>>(x.TargetSnapshotsJson) ?? [];
    private static ScreenshotGenerationDto Map(ScreenshotCodeGeneration x, IReadOnlyList<TargetSnapshot> targets) => new(x.ID, x.ProjectId, x.Prompt, x.ImageFileName,
        x.ImageMediaType, x.ImageHash, x.Status, x.Analysis, x.PreviewHtml,
        targets.Select(t => new ScreenshotFileDto(t.Path, t.NodeId, t.Content, t.Path == "App.tsx" ? x.AppTsx : x.StylesCss, t.Token)).ToList(),
        x.ModelProvider, x.ModelName, x.FailureReason, x.GeneratedAt, x.AppliedAt);
    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
}
