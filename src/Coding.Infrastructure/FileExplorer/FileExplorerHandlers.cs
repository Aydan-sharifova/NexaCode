using System.Data;
using System.Security.Cryptography;
using System.Text;
using Coding.Application.Abstractions;
using Coding.Application.Features.FileExplorer;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Infrastructure.Repositories;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.FileExplorer;

internal static class NodeOperations
{
    public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    public static string Hash(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));
    public static async Task EnsureUniqueAsync(AppDbContext db, Guid projectId, Guid? parentId, string name, Guid? excludedId, bool includeDeleted, CancellationToken ct)
    {
        var query = includeDeleted ? db.WorkspaceNodes.IgnoreQueryFilters() : db.WorkspaceNodes;
        if (await query.AnyAsync(n => n.ProjectId == projectId && n.ParentId == parentId && n.ID != excludedId && !n.IsDeleted && n.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("A sibling with this name already exists.");
    }
    public static async Task<WorkspaceNode> NodeAsync(AppDbContext db, Guid id, CancellationToken ct, bool deleted = false) =>
        await (deleted ? db.WorkspaceNodes.IgnoreQueryFilters() : db.WorkspaceNodes).SingleOrDefaultAsync(n => n.ID == id, ct) ?? throw new NotFoundException("Workspace node not found.");
    public static async Task<string> PathAsync(AppDbContext db, WorkspaceNode node, CancellationToken ct)
    {
        var parts = new Stack<string>(); var current = node; var seen = new HashSet<Guid>();
        while (true) { if (!seen.Add(current.ID)) throw new ConflictException("The node hierarchy contains a cycle."); parts.Push(current.Name); if (!current.ParentId.HasValue) break; current = await db.WorkspaceNodes.IgnoreQueryFilters().AsNoTracking().SingleAsync(n => n.ID == current.ParentId.Value, ct); }
        return "/" + string.Join('/', parts);
    }
    public static async Task<WorkspaceNodeDto> MapAsync(AppDbContext db, WorkspaceNode node, CancellationToken ct) => new(node.ID, node.ProjectId, node.ParentId, node.Name, node.NodeType, await PathAsync(db, node, ct), await db.WorkspaceNodes.AnyAsync(n => n.ParentId == node.ID, ct), node.CreatAt);
    public static async Task<List<WorkspaceNode>> DescendantsAsync(AppDbContext db, Guid rootId, bool ignored, CancellationToken ct)
    {
        var all = await (ignored ? db.WorkspaceNodes.IgnoreQueryFilters() : db.WorkspaceNodes).Where(n => n.ID == rootId || n.ParentId != null).ToListAsync(ct);
        var result = new List<WorkspaceNode>(); var queue = new Queue<Guid>(); queue.Enqueue(rootId);
        while (queue.Count > 0) { var id = queue.Dequeue(); foreach (var child in all.Where(n => n.ParentId == id)) { result.Add(child); queue.Enqueue(child.ID); } }
        return result;
    }
    public static async Task EnsureParentAsync(AppDbContext db, Guid projectId, Guid? parentId, CancellationToken ct)
    {
        if (!parentId.HasValue) return;
        var parent = await NodeAsync(db, parentId.Value, ct);
        if (parent.ProjectId != projectId) throw new ConflictException("Nodes cannot move between projects.");
        if (parent.NodeType != WorkspaceNodeType.Folder) throw new ConflictException("A file cannot contain child nodes.");
    }
    public static async Task<FileContentDto> SaveAsync(AppDbContext db, ICurrentUser user, WorkspaceNode node, string content, string concurrencyToken, CancellationToken ct)
    {
        if (node.NodeType != WorkspaceNodeType.File) throw new ConflictException("Only files contain editable content.");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var state = await db.FileContents.SingleAsync(x => x.NodeId == node.ID, ct); var hash = Hash(content);
            if (!string.Equals(state.ConcurrencyToken, concurrencyToken, StringComparison.Ordinal)) throw new ConflictException("The file was updated by another client. Reload before saving.");
            if (state.ContentHash == hash && !state.IsBinary) { await tx.CommitAsync(ct); return new FileContentDto(node.ID, await PathAsync(db, node, ct), state.Content, false, state.ContentHash, state.ConcurrencyToken, state.VersionNumber, state.UpdatedAt); }
            state.Content = content; state.IsBinary = false; state.BinaryContent = null; state.ContentHash = hash; state.ConcurrencyToken = Guid.NewGuid().ToString("N"); state.VersionNumber++; state.UpdatedAt = DateTime.UtcNow; state.UpdatedById = user.UserId;
            db.FileVersions.Add(new FileVersion { ID = Guid.NewGuid(), NodeId = node.ID, VersionNumber = state.VersionNumber, Content = content, IsBinary = false, ContentHash = hash, CreatedById = user.UserId, CreatAt = state.UpdatedAt });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new FileContentDto(node.ID, await PathAsync(db, node, ct), content, false, hash, state.ConcurrencyToken, state.VersionNumber, state.UpdatedAt);
        });
    }
}

public sealed class CreateFolderHandler(AppDbContext db, ICurrentUser user, IProjectRepositoryCoordinator coordinator) : IRequestHandler<CreateFolderCommand, WorkspaceNodeDto>
{ public async Task<WorkspaceNodeDto> Handle(CreateFolderCommand r, CancellationToken ct) { await ProjectAccess.RequireWorkspaceWriteAsync(db, r.ProjectId, user.UserId, ct); await using var lease = await coordinator.AcquireAsync(r.ProjectId, ct); await NodeOperations.EnsureParentAsync(db, r.ProjectId, r.ParentId, ct); await NodeOperations.EnsureUniqueAsync(db, r.ProjectId, r.ParentId, r.Name.Trim(), null, false, ct); var n = new WorkspaceNode { ID = Guid.NewGuid(), ProjectId = r.ProjectId, ParentId = r.ParentId, Name = r.Name.Trim(), NodeType = WorkspaceNodeType.Folder }; db.WorkspaceNodes.Add(n); await db.SaveChangesAsync(ct); return await NodeOperations.MapAsync(db, n, ct); } }

public sealed class CreateFileHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<CreateFileCommand, WorkspaceNodeDto>
{
    public async Task<WorkspaceNodeDto> Handle(CreateFileCommand r, CancellationToken ct)
    {
        await ProjectAccess.RequireWorkspaceWriteAsync(db, r.ProjectId, user.UserId, ct); await NodeOperations.EnsureParentAsync(db, r.ProjectId, r.ParentId, ct); await NodeOperations.EnsureUniqueAsync(db, r.ProjectId, r.ParentId, r.Name.Trim(), null, false, ct);
        await using var lease = await coordinator.AcquireAsync(r.ProjectId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct); var now = DateTime.UtcNow; var hash = NodeOperations.Hash(r.Content);
            var n = new WorkspaceNode { ID = Guid.NewGuid(), ProjectId = r.ProjectId, ParentId = r.ParentId, Name = r.Name.Trim(), NodeType = WorkspaceNodeType.File };
            db.WorkspaceNodes.Add(n); db.FileContents.Add(new FileContent { Node = n, Content = r.Content, ContentHash = hash, ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 1, UpdatedAt = now, UpdatedById = user.UserId }); db.FileVersions.Add(new FileVersion { ID = Guid.NewGuid(), Node = n, Content = r.Content, ContentHash = hash, VersionNumber = 1, CreatedById = user.UserId, CreatAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await NodeOperations.MapAsync(db, n, ct);
        });
        await RepositoryMaterializer.SynchronizeAsync(db, git, r.ProjectId, ct);
        return result;
    }
}

public sealed class RenameNodeHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<RenameNodeCommand, WorkspaceNodeDto>
{ public async Task<WorkspaceNodeDto> Handle(RenameNodeCommand r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireWorkspaceWriteAsync(db, n.ProjectId, user.UserId, ct); await using var lease = await coordinator.AcquireAsync(n.ProjectId, ct); await NodeOperations.EnsureUniqueAsync(db, n.ProjectId, n.ParentId, r.Name.Trim(), n.ID, false, ct); n.Name = r.Name.Trim(); n.UpdateAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await RepositoryMaterializer.SynchronizeAsync(db, git, n.ProjectId, ct); return await NodeOperations.MapAsync(db, n, ct); } }

public sealed class MoveNodeHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<MoveNodeCommand, WorkspaceNodeDto>
{ public async Task<WorkspaceNodeDto> Handle(MoveNodeCommand r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireWorkspaceWriteAsync(db, n.ProjectId, user.UserId, ct); await using var lease = await coordinator.AcquireAsync(n.ProjectId, ct); if (r.ParentId == n.ID) throw new ConflictException("A folder cannot be moved inside itself."); await NodeOperations.EnsureParentAsync(db, n.ProjectId, r.ParentId, ct); if (r.ParentId.HasValue && (await NodeOperations.DescendantsAsync(db, n.ID, false, ct)).Any(x => x.ID == r.ParentId)) throw new ConflictException("A folder cannot be moved into one of its descendants."); await NodeOperations.EnsureUniqueAsync(db, n.ProjectId, r.ParentId, n.Name, n.ID, false, ct); n.ParentId = r.ParentId; n.UpdateAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await RepositoryMaterializer.SynchronizeAsync(db, git, n.ProjectId, ct); return await NodeOperations.MapAsync(db, n, ct); } }

public sealed class DeleteNodeHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<DeleteNodeCommand>
{ public async Task Handle(DeleteNodeCommand r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); var role = await ProjectAccess.RequireWorkspaceWriteAsync(db, n.ProjectId, user.UserId, ct); await using var lease = await coordinator.AcquireAsync(n.ProjectId, ct); if (n.NodeType == WorkspaceNodeType.Folder) ProjectAccess.RequireManager(role); var now = DateTime.UtcNow; foreach (var item in (await NodeOperations.DescendantsAsync(db, n.ID, false, ct)).Prepend(n)) { item.IsDeleted = true; item.DeletedAt = item.UpdateAt = now; } await db.SaveChangesAsync(ct); await RepositoryMaterializer.SynchronizeAsync(db, git, n.ProjectId, ct); } }

public sealed class RestoreDeletedNodeHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<RestoreDeletedNodeCommand>
{ public async Task Handle(RestoreDeletedNodeCommand r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct, true); var role = await ProjectAccess.RequireMemberAsync(db, n.ProjectId, user.UserId, ct); ProjectAccess.RequireManager(role); await using var lease = await coordinator.AcquireAsync(n.ProjectId, ct); await NodeOperations.EnsureParentAsync(db, n.ProjectId, n.ParentId, ct); await NodeOperations.EnsureUniqueAsync(db, n.ProjectId, n.ParentId, n.Name, n.ID, true, ct); foreach (var item in (await NodeOperations.DescendantsAsync(db, n.ID, true, ct)).Prepend(n)) { item.IsDeleted = false; item.DeletedAt = null; item.UpdateAt = DateTime.UtcNow; } await db.SaveChangesAsync(ct); await RepositoryMaterializer.SynchronizeAsync(db, git, n.ProjectId, ct); } }

public sealed class SaveFileContentHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<SaveFileContentCommand, FileContentDto>
{
    public async Task<FileContentDto> Handle(SaveFileContentCommand r, CancellationToken ct)
    {
        var node = await NodeOperations.NodeAsync(db, r.NodeId, ct);
        await ProjectAccess.RequireWorkspaceWriteAsync(db, node.ProjectId, user.UserId, ct);
        await using var lease = await coordinator.AcquireAsync(node.ProjectId, ct);

        var saved = await NodeOperations.SaveAsync(db, user, node, r.Content, r.ConcurrencyToken, ct);
        await git.InitializeAsync(node.ProjectId, "main", ct);
        await git.WriteFileAsync(node.ProjectId, saved.Path.TrimStart('/'), Encoding.UTF8.GetBytes(saved.Content), ct);
        return saved;
    }
}

public sealed class SaveBinaryFileContentHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<SaveBinaryFileContentCommand>
{
    public async Task Handle(SaveBinaryFileContentCommand r, CancellationToken ct)
    {
        var node = await NodeOperations.NodeAsync(db, r.NodeId, ct);
        await ProjectAccess.RequireWorkspaceWriteAsync(db, node.ProjectId, user.UserId, ct);
        await using var lease = await coordinator.AcquireAsync(node.ProjectId, ct);
        if (node.NodeType != WorkspaceNodeType.File) throw new ConflictException("Only files contain binary content.");
        var state = await db.FileContents.SingleAsync(content => content.NodeId == node.ID, ct);
        var hash = NodeOperations.Hash(r.Content);
        if (state.ContentHash != hash || !state.IsBinary)
        {
            var now = DateTime.UtcNow;
            state.Content = string.Empty; state.IsBinary = true; state.BinaryContent = r.Content; state.ContentHash = hash;
            state.ConcurrencyToken = Guid.NewGuid().ToString("N"); state.VersionNumber++; state.UpdatedAt = now; state.UpdatedById = user.UserId;
            db.FileVersions.Add(new FileVersion { ID = Guid.NewGuid(), NodeId = node.ID, VersionNumber = state.VersionNumber, Content = string.Empty, IsBinary = true, BinaryContent = r.Content, ContentHash = hash, CreatedById = user.UserId, CreatAt = now });
            await db.SaveChangesAsync(ct);
        }
        await git.InitializeAsync(node.ProjectId, "main", ct);
        await git.WriteFileAsync(node.ProjectId, (await NodeOperations.PathAsync(db, node, ct)).TrimStart('/'), r.Content, ct);
    }
}

public sealed class RestoreFileVersionHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<RestoreFileVersionCommand, FileContentDto>
{ public async Task<FileContentDto> Handle(RestoreFileVersionCommand r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireWorkspaceWriteAsync(db, n.ProjectId, user.UserId, ct); var version = await db.FileVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ID == r.VersionId && x.NodeId == n.ID, ct) ?? throw new NotFoundException("File version not found."); if (version.IsBinary) { await new SaveBinaryFileContentHandler(db, user, git, coordinator).Handle(new SaveBinaryFileContentCommand(n.ID, version.BinaryContent ?? []), ct); var binaryState = await db.FileContents.AsNoTracking().SingleAsync(x => x.NodeId == n.ID, ct); return new(n.ID, await NodeOperations.PathAsync(db, n, ct), string.Empty, true, binaryState.ContentHash, binaryState.ConcurrencyToken, binaryState.VersionNumber, binaryState.UpdatedAt); } await using var lease = await coordinator.AcquireAsync(n.ProjectId, ct); var token = await db.FileContents.Where(x => x.NodeId == n.ID).Select(x => x.ConcurrencyToken).SingleAsync(ct); var restored = await NodeOperations.SaveAsync(db, user, n, version.Content, token, ct); await git.InitializeAsync(n.ProjectId, "main", ct); await git.WriteFileAsync(n.ProjectId, restored.Path.TrimStart('/'), Encoding.UTF8.GetBytes(restored.Content), ct); return restored; } }

public sealed class GetProjectFileTreeHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetProjectFileTreeQuery, IReadOnlyList<WorkspaceNodeDto>>
{ public async Task<IReadOnlyList<WorkspaceNodeDto>> Handle(GetProjectFileTreeQuery r, CancellationToken ct) { await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct); var nodes = await db.WorkspaceNodes.AsNoTracking().Where(n => n.ProjectId == r.ProjectId).OrderBy(n => n.NodeType).ThenBy(n => n.Name).ToListAsync(ct); var result = new List<WorkspaceNodeDto>(); foreach (var n in nodes) result.Add(await NodeOperations.MapAsync(db, n, ct)); return result; } }

public sealed class GetFolderChildrenHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetFolderChildrenQuery, IReadOnlyList<WorkspaceNodeDto>>
{ public async Task<IReadOnlyList<WorkspaceNodeDto>> Handle(GetFolderChildrenQuery r, CancellationToken ct) { await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct); if (r.ParentId.HasValue) await NodeOperations.EnsureParentAsync(db, r.ProjectId, r.ParentId, ct); var nodes = await db.WorkspaceNodes.AsNoTracking().Where(n => n.ProjectId == r.ProjectId && n.ParentId == r.ParentId).OrderBy(n => n.NodeType).ThenBy(n => n.Name).ToListAsync(ct); var result = new List<WorkspaceNodeDto>(); foreach (var n in nodes) result.Add(await NodeOperations.MapAsync(db, n, ct)); return result; } }

public sealed class GetFileContentHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetFileContentQuery, FileContentDto>
{ public async Task<FileContentDto> Handle(GetFileContentQuery r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireMemberAsync(db, n.ProjectId, user.UserId, ct); var c = await db.FileContents.AsNoTracking().SingleOrDefaultAsync(x => x.NodeId == n.ID, ct) ?? throw new NotFoundException("File content not found."); return new(n.ID, await NodeOperations.PathAsync(db, n, ct), c.Content, c.IsBinary, c.ContentHash, c.ConcurrencyToken, c.VersionNumber, c.UpdatedAt); } }

public sealed class GetNodeDetailsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetNodeDetailsQuery, WorkspaceNodeDto>
{ public async Task<WorkspaceNodeDto> Handle(GetNodeDetailsQuery r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireMemberAsync(db, n.ProjectId, user.UserId, ct); return await NodeOperations.MapAsync(db, n, ct); } }

public sealed class GetFileVersionsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetFileVersionsQuery, IReadOnlyList<FileVersionDto>>
{ public async Task<IReadOnlyList<FileVersionDto>> Handle(GetFileVersionsQuery r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireMemberAsync(db, n.ProjectId, user.UserId, ct); return await db.FileVersions.AsNoTracking().Where(x => x.NodeId == r.NodeId).OrderByDescending(x => x.VersionNumber).Select(x => new FileVersionDto(x.ID, x.NodeId, x.VersionNumber, x.ContentHash, x.CreatedById, x.CreatedBy.FirstName + " " + x.CreatedBy.LastName, x.CreatAt)).ToListAsync(ct); } }

public sealed class GetFileVersionByIdHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetFileVersionByIdQuery, FileVersionDetails>
{ public async Task<FileVersionDetails> Handle(GetFileVersionByIdQuery r, CancellationToken ct) { var n = await NodeOperations.NodeAsync(db, r.NodeId, ct); await ProjectAccess.RequireMemberAsync(db, n.ProjectId, user.UserId, ct); return await db.FileVersions.AsNoTracking().Where(x => x.NodeId == r.NodeId && x.ID == r.VersionId).Select(x => new FileVersionDetails(x.ID, x.NodeId, x.VersionNumber, x.Content, x.IsBinary, x.ContentHash, x.CreatedById, x.CreatedBy.FirstName + " " + x.CreatedBy.LastName, x.CreatAt)).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("File version not found."); } }

public sealed class CompareFileVersionsHandler(ISender sender) : IRequestHandler<CompareFileVersionsQuery, VersionComparison>
{ public async Task<VersionComparison> Handle(CompareFileVersionsQuery r, CancellationToken ct) { var left = await sender.Send(new GetFileVersionByIdQuery(r.NodeId, r.LeftId), ct); var right = await sender.Send(new GetFileVersionByIdQuery(r.NodeId, r.RightId), ct); return new(left, right, left.ContentHash == right.ContentHash); } }
