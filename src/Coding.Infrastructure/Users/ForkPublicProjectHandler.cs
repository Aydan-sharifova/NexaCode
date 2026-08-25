using System.Data;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Users;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Users;

public sealed class ForkPublicProjectHandler(AppDbContext db, ICurrentUser current, IActivityLogger activity) : IRequestHandler<ForkPublicProjectCommand, ForkPublicProjectResult>
{
    private const int MaximumNodes = 1_000;
    private const long MaximumBytes = 10 * 1024 * 1024;

    public async Task<ForkPublicProjectResult> Handle(ForkPublicProjectCommand request, CancellationToken ct)
    {
        var source = await db.Projects.AsNoTracking().Where(x => x.ID == request.ProjectId && x.IsPublic &&
            !db.UserBlocks.Any(block => block.BlockerId == current.UserId && block.BlockedId == x.OwnerId || block.BlockerId == x.OwnerId && block.BlockedId == current.UserId))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Public project not found.");
        var sourceNodes = await db.WorkspaceNodes.AsNoTracking().Include(x => x.FileContent).Where(x => x.ProjectId == source.ID).ToListAsync(ct);
        var totalBytes = sourceNodes.Where(x => x.FileContent is not null).Sum(x => x.FileContent!.IsBinary ? x.FileContent.BinaryContent?.LongLength ?? 0 : System.Text.Encoding.UTF8.GetByteCount(x.FileContent.Content));
        if (sourceNodes.Count > MaximumNodes || totalBytes > MaximumBytes) throw new ConflictException("This repository exceeds the 1,000 node or 10 MB fork limit.");
        var byId = sourceNodes.ToDictionary(x => x.ID);
        int Depth(WorkspaceNode node, HashSet<Guid> visiting)
        {
            if (!visiting.Add(node.ID)) throw new ConflictException("The source workspace hierarchy contains a cycle.");
            var depth = node.ParentId.HasValue && byId.TryGetValue(node.ParentId.Value, out var parent) ? 1 + Depth(parent, visiting) : 0;
            visiting.Remove(node.ID);
            return depth;
        }
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var fork = new Project { ID = Guid.NewGuid(), Name = $"{source.Name} Fork", Description = source.Description, OwnerId = current.UserId, DefaultLanguage = source.DefaultLanguage, IsPublic = false, CreatedAt = now, CreatAt = now, Status = ProjectStatus.Active, ProtectedBranch = source.ProtectedBranch, RequiredPullRequestApprovals = source.RequiredPullRequestApprovals, RequirePassingPullRequestTests = source.RequirePassingPullRequestTests, ForkedFromProjectId = source.ID };
        db.Projects.Add(fork);
        db.ProjectMembers.Add(new ProjectMember { ID = Guid.NewGuid(), ProjectId = fork.ID, UserId = current.UserId, Role = ProjectRole.Owner, JoinedAt = now, CreatAt = now });
        var map = new Dictionary<Guid, WorkspaceNode>();
        foreach (var sourceNode in sourceNodes.OrderBy(x => Depth(x, [])))
        {
            var clone = new WorkspaceNode { ID = Guid.NewGuid(), ProjectId = fork.ID, ParentId = sourceNode.ParentId.HasValue ? map[sourceNode.ParentId.Value].ID : null, Name = sourceNode.Name, NodeType = sourceNode.NodeType, CreatAt = now, UpdateAt = now };
            map[sourceNode.ID] = clone;
            db.WorkspaceNodes.Add(clone);
            if (sourceNode.FileContent is null) continue;
            var content = sourceNode.FileContent;
            clone.FileContent = new FileContent { NodeId = clone.ID, Content = content.Content, IsBinary = content.IsBinary, BinaryContent = content.BinaryContent?.ToArray(), ContentHash = content.ContentHash, ConcurrencyToken = Guid.NewGuid().ToString("N"), VersionNumber = 1, UpdatedAt = now, UpdatedById = current.UserId };
            db.FileVersions.Add(new FileVersion { ID = Guid.NewGuid(), NodeId = clone.ID, Content = content.Content, IsBinary = content.IsBinary, BinaryContent = content.BinaryContent?.ToArray(), ContentHash = content.ContentHash, VersionNumber = 1, CreatedById = current.UserId, CreatAt = now });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await activity.LogAsync(new(current.UserId, fork.ID, "ProjectForked", nameof(Project), fork.ID, $"Forked public project '{source.Name}'.", new Dictionary<string, object?> { ["sourceProjectId"] = source.ID }), ct);
        return new(fork.ID, source.ID);
    }
}
