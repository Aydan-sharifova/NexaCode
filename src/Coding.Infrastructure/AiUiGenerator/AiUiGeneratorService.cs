using System.Data;
using System.Text;
using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.AiUiGenerator;
using Coding.Application.Features.AiAgent;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.FileExplorer;
using Coding.Infrastructure.Projects;
using Coding.Infrastructure.Repositories;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiUiGenerator;

public sealed class AiUiGeneratorService(AppDbContext db, ICurrentUser currentUser, IAiProvider provider,
    IProjectRepositoryCoordinator coordinator, IGitRepositoryService git, IAiSecretRedactionService redaction) : IAiUiGeneratorService
{
    private sealed record GeneratedFile(string Path, string Content);
    private sealed record TargetSnapshot(string Path, Guid? NodeId, string? Content, string? Hash, string? Token);

    public async Task<AiUiGenerationDto> GenerateAsync(Guid projectId, string prompt, bool includeSampleData, CancellationToken ct)
    {
        await ProjectAccess.RequireWorkspaceWriteAsync(db, projectId, currentUser.UserId, ct);
        prompt = prompt.Trim();
        if (prompt.Length is < 10 or > 2000) throw new ArgumentException("Describe the interface in 10 to 2,000 characters.");
        var targets = await LoadTargets(projectId, ct);
        if (targets.Where(x => x.Content is not null).Any(x => redaction.Redact(x.Content!) != x.Content))
            throw new ConflictException("AI UI generation stopped because a target file contains secret-shaped data. Move secrets to protected configuration before generating UI.");
        var sampleRule = includeSampleData
            ? "Use clearly labeled non-production sample data only where it materially demonstrates the requested interface."
            : "Do not invent or include sample records, user names, metrics, transactions, charts, or other sample data. Render honest empty/loading-ready states instead.";
        var request = new AiRequest(
            "You are a production UI generator. Return exactly the requested marked sections, no markdown fences. Generate accessible responsive React TypeScript and plain CSS without remote resources. Do not claim functionality that is absent.",
            "Create the requested UI with a component layer, page layer, routing/composition, responsive layout, coherent visual system, focus/hover/empty states and bounded interactions. " + sampleRule +
            " Return [[[ANALYSIS]]], [[[APP_TSX]]], [[[DASHBOARD_PAGE_TSX]]], [[[DASHBOARD_SHELL_TSX]]], [[[STYLES_CSS]]], and [[[PREVIEW_HTML]]]. PREVIEW_HTML must be standalone with embedded CSS and no remote URLs. Request: " + prompt,
            string.Join("\n\n", targets.Where(x => x.Content is not null).Select(x => $"Existing {x.Path}:\n{Limit(redaction.Redact(x.Content!), 30000)}")),
            "typescript", AiAssistantAction.GenerateCode, [], MaxOutputTokens: 8192);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var output = new StringBuilder();
        try { await foreach (var chunk in provider.StreamAsync(request, timeout.Token)) output.Append(chunk.Content); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("Ollama UI generation exceeded the 90-second limit."); }
        var raw = output.ToString();
        var files = AiUiGeneratorPolicy.Sections.ToDictionary(x => x.Key, x => ScreenshotToCode.ScreenshotCodePolicy.ExtractSection(raw, x.Value));
        AiUiGeneratorPolicy.ValidateFiles(files);
        AiUiGeneratorPolicy.ValidateSampleDataBoundary(files.Values, includeSampleData);
        var preview = ScreenshotToCode.ScreenshotCodePolicy.ExtractSection(raw, "PREVIEW_HTML");
        ScreenshotToCode.ScreenshotCodePolicy.ValidateGenerated(files["src/App.tsx"], files["src/styles.css"], preview);
        preview = ScreenshotToCode.ScreenshotCodePolicy.SecurePreview(preview);
        var now = DateTime.UtcNow;
        var entity = new AiUiGeneration
        {
            ID = Guid.NewGuid(), ProjectId = projectId, UserId = currentUser.UserId, Prompt = prompt, IncludeSampleData = includeSampleData,
            Status = ScreenshotGenerationStatus.Draft, Analysis = Limit(ScreenshotToCode.ScreenshotCodePolicy.ExtractSection(raw, "ANALYSIS"), 8000),
            PreviewHtml = preview, FilesJson = JsonSerializer.Serialize(files.Select(x => new GeneratedFile(x.Key, x.Value))),
            TargetSnapshotsJson = JsonSerializer.Serialize(targets), ModelProvider = provider.ProviderName, ModelName = provider.Model,
            GeneratedAt = now, CreatAt = now
        };
        db.AiUiGenerations.Add(entity); await db.SaveChangesAsync(ct); return Map(entity, targets, files);
    }

    public async Task<IReadOnlyList<AiUiGenerationDto>> ListAsync(Guid projectId, int take, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct);
        var rows = await db.AiUiGenerations.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.GeneratedAt)
            .Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<AiUiGenerationDto> GetAsync(Guid projectId, Guid id, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct); return Map(await Find(projectId, id, ct));
    }

    public async Task<AiUiGenerationDto> ApplyAsync(Guid projectId, Guid id, bool confirm, CancellationToken ct)
    {
        if (!confirm) throw new ArgumentException("Explicit confirmation is required before generated UI files are written.");
        await ProjectAccess.RequireWorkspaceWriteAsync(db, projectId, currentUser.UserId, ct);
        await using var lease = await coordinator.AcquireAsync(projectId, ct);
        var entity = await Find(projectId, id, ct);
        if (entity.Status != ScreenshotGenerationStatus.Draft) throw new ConflictException("Only an unapplied UI draft can be applied.");
        var targets = ReadTargets(entity); var generated = ReadFiles(entity);
        var currentNodes = await db.WorkspaceNodes.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(ct);
        var currentPaths = BuildPaths(currentNodes);
        foreach (var target in targets.Where(x => x.NodeId.HasValue))
        {
            if (!currentPaths.TryGetValue(target.NodeId!.Value, out var currentPath) || currentPath != target.Path)
                throw new ConflictException($"{target.Path} was moved or renamed after generation. Generate a fresh draft before applying.");
            var state = await db.FileContents.AsNoTracking().SingleOrDefaultAsync(x => x.NodeId == target.NodeId, ct);
            if (state is null || state.IsBinary || state.ContentHash != target.Hash || state.ConcurrencyToken != target.Token)
                throw new ConflictException($"{target.Path} changed after generation. Generate a fresh draft before applying.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var nodes = await db.WorkspaceNodes.Where(x => x.ProjectId == projectId).ToListAsync(ct);
        var src = EnsureFolder(nodes, projectId, null, "src");
        var pages = EnsureFolder(nodes, projectId, src.ID, "pages");
        var components = EnsureFolder(nodes, projectId, src.ID, "components");
        foreach (var folder in new[] { src, pages, components })
            if (db.Entry(folder).State == EntityState.Detached) db.WorkspaceNodes.Add(folder);
        var parents = new Dictionary<string,Guid>{{"src/App.tsx",src.ID},{"src/styles.css",src.ID},{"src/pages/DashboardPage.tsx",pages.ID},{"src/components/DashboardShell.tsx",components.ID}};
        var now = DateTime.UtcNow;
        foreach (var file in generated)
        {
            var target = targets.Single(x => x.Path == file.Path);
            if (target.NodeId.HasValue)
            {
                var state = await db.FileContents.SingleAsync(x => x.NodeId == target.NodeId, ct);
                state.Content = file.Content; state.IsBinary = false; state.BinaryContent = null; state.ContentHash = NodeOperations.Hash(file.Content);
                state.ConcurrencyToken = Guid.NewGuid().ToString("N"); state.VersionNumber++; state.UpdatedAt = now; state.UpdatedById = currentUser.UserId;
                db.FileVersions.Add(new FileVersion { ID=Guid.NewGuid(), NodeId=target.NodeId.Value, VersionNumber=state.VersionNumber, Content=file.Content, ContentHash=state.ContentHash, CreatedById=currentUser.UserId, CreatAt=now });
            }
            else
            {
                var name = file.Path.Split('/').Last();
                if (nodes.Any(x => x.ParentId == parents[file.Path] && x.Name.Equals(name,StringComparison.OrdinalIgnoreCase))) throw new ConflictException($"{file.Path} was created after this draft. Generate again.");
                var node = new WorkspaceNode { ID=Guid.NewGuid(), ProjectId=projectId, ParentId=parents[file.Path], Name=name, NodeType=WorkspaceNodeType.File, CreatAt=now };
                nodes.Add(node); db.WorkspaceNodes.Add(node); var hash=NodeOperations.Hash(file.Content);
                db.FileContents.Add(new FileContent { Node=node, Content=file.Content, ContentHash=hash, ConcurrencyToken=Guid.NewGuid().ToString("N"), VersionNumber=1, UpdatedAt=now, UpdatedById=currentUser.UserId });
                db.FileVersions.Add(new FileVersion { ID=Guid.NewGuid(), Node=node, Content=file.Content, ContentHash=hash, VersionNumber=1, CreatedById=currentUser.UserId, CreatAt=now });
            }
        }
        entity.Status=ScreenshotGenerationStatus.Applied; entity.AppliedAt=entity.UpdateAt=now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await RepositoryMaterializer.SynchronizeAsync(db,git,projectId,ct);
        return Map(entity, targets, generated.ToDictionary(x=>x.Path,x=>x.Content));
    }

    private static WorkspaceNode EnsureFolder(List<WorkspaceNode> nodes, Guid projectId, Guid? parentId, string name)
    {
        var found=nodes.SingleOrDefault(x=>x.ParentId==parentId&&x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
        if(found is not null){if(found.NodeType!=WorkspaceNodeType.Folder)throw new ConflictException($"Cannot create folder {name}; a file occupies that path.");return found;}
        var node=new WorkspaceNode{ID=Guid.NewGuid(),ProjectId=projectId,ParentId=parentId,Name=name,NodeType=WorkspaceNodeType.Folder,CreatAt=DateTime.UtcNow};nodes.Add(node);return node;
    }
    private async Task<List<TargetSnapshot>> LoadTargets(Guid projectId,CancellationToken ct){var nodes=await db.WorkspaceNodes.AsNoTracking().Where(x=>x.ProjectId==projectId).ToListAsync(ct);var contents=await db.FileContents.AsNoTracking().Where(x=>x.Node.ProjectId==projectId).ToDictionaryAsync(x=>x.NodeId,ct);var paths=BuildPaths(nodes);return AiUiGeneratorPolicy.Sections.Keys.Select(path=>{var node=paths.SingleOrDefault(x=>x.Value==path).Key;if(node==Guid.Empty||!contents.TryGetValue(node,out var state))return new TargetSnapshot(path,null,null,null,null);if(state.IsBinary)throw new ConflictException($"{path} is binary and cannot be replaced by generated UI code.");return new TargetSnapshot(path,node,state.Content,state.ContentHash,state.ConcurrencyToken);}).ToList();}
    private static Dictionary<Guid,string> BuildPaths(IReadOnlyList<WorkspaceNode> nodes){var byId=nodes.ToDictionary(x=>x.ID);var result=new Dictionary<Guid,string>();foreach(var node in nodes){var parts=new Stack<string>();var current=node;var seen=new HashSet<Guid>();while(true){if(!seen.Add(current.ID))throw new ConflictException("Workspace hierarchy contains a cycle.");parts.Push(current.Name);if(!current.ParentId.HasValue)break;if(!byId.TryGetValue(current.ParentId.Value,out current!))throw new ConflictException("Workspace hierarchy contains a missing parent.");}result[node.ID]=string.Join('/',parts);}return result;}
    private async Task<AiUiGeneration> Find(Guid projectId,Guid id,CancellationToken ct)=>await db.AiUiGenerations.SingleOrDefaultAsync(x=>x.ID==id&&x.ProjectId==projectId,ct)??throw new NotFoundException("AI UI generation not found.");
    private static List<TargetSnapshot> ReadTargets(AiUiGeneration x)=>JsonSerializer.Deserialize<List<TargetSnapshot>>(x.TargetSnapshotsJson)??[];
    private static List<GeneratedFile> ReadFiles(AiUiGeneration x)=>JsonSerializer.Deserialize<List<GeneratedFile>>(x.FilesJson)??[];
    private static AiUiGenerationDto Map(AiUiGeneration x)=>Map(x,ReadTargets(x),ReadFiles(x).ToDictionary(y=>y.Path,y=>y.Content));
    private static AiUiGenerationDto Map(AiUiGeneration x,IReadOnlyList<TargetSnapshot> targets,IReadOnlyDictionary<string,string> files)=>new(x.ID,x.ProjectId,x.Prompt,x.IncludeSampleData,x.Status,x.Analysis,x.PreviewHtml,targets.Select(t=>new AiUiFileDto(t.Path,t.NodeId,t.Content,files[t.Path],t.Token)).ToList(),x.ModelProvider,x.ModelName,x.GeneratedAt,x.AppliedAt);
    private static string Limit(string x,int max)=>x.Length<=max?x:x[..max];
}
