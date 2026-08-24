using System.Data;
using System.Text;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.FileExplorer;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Repositories;

public static class BranchWorkspaceSynchronizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task ImportAsync(AppDbContext db, Guid projectId, Guid actorId,
        IReadOnlyList<GitBranchFile> snapshot, CancellationToken ct)
    {
        var decoded = snapshot.Select(Decode).ToArray();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var existing = await db.WorkspaceNodes.IgnoreQueryFilters().Include(node => node.FileContent)
                .Where(node => node.ProjectId == projectId).ToListAsync(ct);
            var paths = BuildPaths(existing);
            var byPath = existing.GroupBy(node => paths[node.ID], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(node => node.IsDeleted).ThenByDescending(node => node.UpdateAt ?? node.CreatAt).ToList(), StringComparer.Ordinal);
            var selected = new Dictionary<string, WorkspaceNode>(StringComparer.Ordinal);
            var desiredDirectories = decoded.SelectMany(file => ParentPaths(file.Path)).Distinct(StringComparer.Ordinal)
                .OrderBy(path => path.Count(character => character == '/')).ThenBy(path => path, StringComparer.Ordinal);

            foreach (var directory in desiredDirectories)
                selected[directory] = SelectOrCreate(directory, WorkspaceNodeType.Folder, projectId, Parent(directory), selected, byPath, db);
            foreach (var file in decoded)
            {
                var node = SelectOrCreate(file.Path, WorkspaceNodeType.File, projectId, Parent(file.Path), selected, byPath, db);
                selected[file.Path] = node;
                ApplyContent(db, node, file, actorId);
            }

            var now = DateTime.UtcNow;
            var desiredIds = selected.Values.Select(node => node.ID).ToHashSet();
            foreach (var node in existing)
            {
                var shouldExist = desiredIds.Contains(node.ID);
                if (node.IsDeleted == !shouldExist) continue;
                node.IsDeleted = !shouldExist;
                node.DeletedAt = shouldExist ? null : now;
                node.UpdateAt = now;
            }
            foreach (var node in selected.Values.Where(node => node.IsDeleted))
            {
                node.IsDeleted = false;
                node.DeletedAt = null;
                node.UpdateAt = now;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    private static SnapshotFile Decode(GitBranchFile file)
    {
        try { return new SnapshotFile(file.Path, StrictUtf8.GetString(file.Content), false, null); }
        catch (DecoderFallbackException) { return new SnapshotFile(file.Path, string.Empty, true, file.Content); }
    }

    private static WorkspaceNode SelectOrCreate(string path, WorkspaceNodeType type, Guid projectId, string? parentPath,
        IReadOnlyDictionary<string, WorkspaceNode> selected, IReadOnlyDictionary<string, List<WorkspaceNode>> byPath, AppDbContext db)
    {
        var parentId = parentPath is null ? (Guid?)null : selected[parentPath].ID;
        var candidate = byPath.GetValueOrDefault(path)?.FirstOrDefault(node => node.NodeType == type && node.ParentId == parentId);
        if (candidate is not null) return candidate;
        var now = DateTime.UtcNow;
        var created = new WorkspaceNode
        {
            ID = Guid.NewGuid(), ProjectId = projectId, ParentId = parentId, Name = Name(path), NodeType = type,
            CreatAt = now, UpdateAt = now
        };
        db.WorkspaceNodes.Add(created);
        return created;
    }

    private static void ApplyContent(AppDbContext db, WorkspaceNode node, SnapshotFile file, Guid actorId)
    {
        var hash = file.IsBinary ? NodeOperations.Hash(file.BinaryContent ?? []) : NodeOperations.Hash(file.Content);
        var now = DateTime.UtcNow;
        if (node.FileContent is null)
        {
            node.FileContent = new FileContent { Node = node, Content = file.Content, IsBinary = file.IsBinary, BinaryContent = file.BinaryContent, ContentHash = hash, ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 1, UpdatedAt = now, UpdatedById = actorId };
            db.FileContents.Add(node.FileContent);
            db.FileVersions.Add(new FileVersion { ID = Guid.NewGuid(), Node = node, Content = file.Content, IsBinary = file.IsBinary, BinaryContent = file.BinaryContent, ContentHash = hash, VersionNumber = 1, CreatedById = actorId, CreatAt = now });
            return;
        }
        if (node.FileContent.ContentHash == hash && node.FileContent.IsBinary == file.IsBinary) return;
        node.FileContent.Content = file.Content;
        node.FileContent.IsBinary = file.IsBinary;
        node.FileContent.BinaryContent = file.BinaryContent;
        node.FileContent.ContentHash = hash;
        node.FileContent.ConcurrencyToken = Guid.NewGuid().ToString("N");
        node.FileContent.VersionNumber++;
        node.FileContent.UpdatedAt = now;
        node.FileContent.UpdatedById = actorId;
        db.FileVersions.Add(new FileVersion { ID = Guid.NewGuid(), NodeId = node.ID, Content = file.Content, IsBinary = file.IsBinary, BinaryContent = file.BinaryContent, ContentHash = hash, VersionNumber = node.FileContent.VersionNumber, CreatedById = actorId, CreatAt = now });
    }

    private static Dictionary<Guid, string> BuildPaths(IReadOnlyCollection<WorkspaceNode> nodes)
    {
        var byId = nodes.ToDictionary(node => node.ID);
        var result = new Dictionary<Guid, string>();
        string Resolve(WorkspaceNode node, HashSet<Guid> visiting)
        {
            if (result.TryGetValue(node.ID, out var cached)) return cached;
            if (!visiting.Add(node.ID)) throw new ConflictException("The workspace hierarchy contains a cycle.");
            var path = node.ParentId.HasValue && byId.TryGetValue(node.ParentId.Value, out var parent)
                ? Resolve(parent, visiting) + "/" + node.Name : node.Name;
            visiting.Remove(node.ID);
            return result[node.ID] = path;
        }
        foreach (var node in nodes) Resolve(node, []);
        return result;
    }

    private static IEnumerable<string> ParentPaths(string path)
    {
        var parts = path.Split('/');
        for (var index = 1; index < parts.Length; index++) yield return string.Join('/', parts.Take(index));
    }

    private static string? Parent(string path) => path.LastIndexOf('/') is var index && index >= 0 ? path[..index] : null;
    private static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
    private sealed record SnapshotFile(string Path, string Content, bool IsBinary, byte[]? BinaryContent);
}
