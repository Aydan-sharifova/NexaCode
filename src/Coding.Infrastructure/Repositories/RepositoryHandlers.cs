using System.Text;
using System.Security.Cryptography;
using Coding.Application.Abstractions;
using Coding.Application.Features.Repositories;
using Coding.Application.Features.Activities;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Repositories;

internal static class RepositoryMaterializer
{
    public static async Task SynchronizeAsync(AppDbContext db, IGitRepositoryService git, Guid projectId, CancellationToken ct)
    {
        await git.InitializeAsync(projectId, "main", ct);

        var nodes = await db.WorkspaceNodes.AsNoTracking()
            .Where(node => node.ProjectId == projectId)
            .Select(node => new { node.ID, node.ParentId, node.Name, node.NodeType })
            .ToListAsync(ct);
        var byId = nodes.ToDictionary(node => node.ID);
        var contents = await db.FileContents
            .Where(content => content.Node.ProjectId == projectId)
            .ToDictionaryAsync(content => content.NodeId, ct);

        var files = new List<GitBranchFile>();
        foreach (var file in nodes.Where(node => node.NodeType == WorkspaceNodeType.File))
        {
            var parts = new Stack<string>();
            var current = file;
            var visited = new HashSet<Guid>();

            while (true)
            {
                if (!visited.Add(current.ID))
                    throw new InvalidOperationException("The workspace hierarchy contains a cycle.");

                parts.Push(current.Name);
                if (!current.ParentId.HasValue) break;
                if (!byId.TryGetValue(current.ParentId.Value, out current))
                    throw new InvalidOperationException("The workspace hierarchy contains a missing parent.");
            }

            if (contents.TryGetValue(file.ID, out var content))
            {
                var path = string.Join('/', parts);
                if (!content.IsBinary && content.Content.Length == 0)
                {
                    try
                    {
                        var legacyBytes = await git.ReadFileAsync(projectId, path, ct);
                        if (legacyBytes.Length > 0 && IsBinary(legacyBytes))
                        {
                            var now = DateTime.UtcNow;
                            content.IsBinary = true; content.BinaryContent = legacyBytes;
                            content.ContentHash = Convert.ToHexString(SHA256.HashData(legacyBytes));
                            content.ConcurrencyToken = Guid.NewGuid().ToString("N"); content.VersionNumber++; content.UpdatedAt = now;
                            db.FileVersions.Add(new Coding.Models.FileVersion { ID = Guid.NewGuid(), NodeId = file.ID, VersionNumber = content.VersionNumber, Content = string.Empty, IsBinary = true, BinaryContent = legacyBytes, ContentHash = content.ContentHash, CreatedById = content.UpdatedById, CreatAt = now });
                        }
                    }
                    catch (IOException) { }
                }
                files.Add(new GitBranchFile(path, content.IsBinary ? content.BinaryContent ?? [] : Encoding.UTF8.GetBytes(content.Content)));
            }
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        await git.ReplaceWorktreeAsync(projectId, files, ct);
    }

    private static bool IsBinary(byte[] content)
    {
        if (content.AsSpan().Contains((byte)0)) return true;
        try { _ = new UTF8Encoding(false, true).GetString(content); return false; }
        catch (DecoderFallbackException) { return true; }
    }
}

public sealed class GetRepositoryStatusHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetRepositoryStatusQuery, GitStatusResult>
{
    public async Task<GitStatusResult> Handle(GetRepositoryStatusQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        return await git.GetStatusAsync(request.ProjectId, cancellationToken);
    }
}

public sealed class GetRepositoryBranchesHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetRepositoryBranchesQuery, IReadOnlyList<GitBranchResult>>
{
    public async Task<IReadOnlyList<GitBranchResult>> Handle(GetRepositoryBranchesQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        return await git.GetBranchesAsync(request.ProjectId, cancellationToken);
    }
}

public sealed class CreateRepositoryBranchHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<CreateRepositoryBranchCommand>
{
    public async Task Handle(CreateRepositoryBranchCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        ProjectAccess.RequireRepositoryWrite(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, cancellationToken);
        await using var lease = await coordinator.AcquireAsync(request.ProjectId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        await git.CreateBranchAsync(request.ProjectId, request.Name, cancellationToken);
    }
}

public sealed class CheckoutRepositoryBranchHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<CheckoutRepositoryBranchCommand>
{
    public async Task Handle(CheckoutRepositoryBranchCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        ProjectAccess.RequireRepositoryWrite(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, cancellationToken);
        await using var lease = await coordinator.AcquireAsync(request.ProjectId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
            var status = await git.GetStatusAsync(request.ProjectId, cancellationToken);
            if (!status.IsClean) throw new ConflictException("Commit or discard workspace changes before switching branches.");
            if (status.CurrentBranch == request.Name) return;
            var snapshot = await git.GetBranchFilesAsync(request.ProjectId, request.Name, cancellationToken);
            await git.CheckoutAsync(request.ProjectId, request.Name, cancellationToken);
            try
            {
                await BranchWorkspaceSynchronizer.ImportAsync(db, request.ProjectId, user.UserId, snapshot, cancellationToken);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(status.CurrentBranch)) await git.CheckoutAsync(request.ProjectId, status.CurrentBranch, CancellationToken.None);
                throw;
            }
    }
}

public sealed class CommitRepositoryChangesHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator, IActivityLogger activity) : IRequestHandler<CommitRepositoryChangesCommand, GitCommitResult>
{
    public async Task<GitCommitResult> Handle(CommitRepositoryChangesCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        ProjectAccess.RequireRepositoryWrite(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, cancellationToken);
        await using var lease = await coordinator.AcquireAsync(request.ProjectId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        var protectedBranch = await db.Projects.Where(project => project.ID == request.ProjectId).Select(project => project.ProtectedBranch).SingleAsync(cancellationToken);
        var status = await git.GetStatusAsync(request.ProjectId, cancellationToken);
        if (status.CurrentBranch == protectedBranch && (await git.GetHistoryAsync(request.ProjectId, 1, cancellationToken)).Count > 0)
            throw new ForbiddenException($"Direct commits to protected branch '{protectedBranch}' are not allowed. Create a branch and merge it through a pull request.");
        await RepositoryMaterializer.SynchronizeAsync(db, git, request.ProjectId, cancellationToken);
        var author = await db.Users.AsNoTracking().Where(item => item.ID == user.UserId)
            .Select(item => new { Name = item.FirstName + " " + item.LastName, item.Email })
            .SingleAsync(cancellationToken);
        var result = await git.CommitAllAsync(request.ProjectId, request.Message, author.Name, author.Email, cancellationToken);
        db.GitCommits.Add(new GitCommit { ID = Guid.NewGuid(), ProjectId = request.ProjectId, UserId = user.UserId, CommitMessage = request.Message.Trim(), CommitHash = result.Sha, CommitDate = result.CommittedAt.UtcDateTime });
        await db.SaveChangesAsync(cancellationToken);
        await activity.LogAsync(new(user.UserId, request.ProjectId, "RepositoryCommit", nameof(GitCommit), null, $"Created commit {result.ShortSha}.", new Dictionary<string, object?> { ["sha"] = result.Sha }), cancellationToken);
        return result;
    }
}

public sealed class StageRepositoryFileHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<StageRepositoryFileCommand>
{
    public async Task Handle(StageRepositoryFileCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        ProjectAccess.RequireRepositoryWrite(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, cancellationToken);
        await using var lease = await coordinator.AcquireAsync(request.ProjectId, cancellationToken);
        await RepositoryMaterializer.SynchronizeAsync(db, git, request.ProjectId, cancellationToken);
        await git.StageAsync(request.ProjectId, request.Path, cancellationToken);
    }
}

public sealed class UnstageRepositoryFileHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git, IProjectRepositoryCoordinator coordinator) : IRequestHandler<UnstageRepositoryFileCommand>
{
    public async Task Handle(UnstageRepositoryFileCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        ProjectAccess.RequireRepositoryWrite(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, request.ProjectId, role, cancellationToken);
        await using var lease = await coordinator.AcquireAsync(request.ProjectId, cancellationToken);
        await git.UnstageAsync(request.ProjectId, request.Path, cancellationToken);
    }
}

public sealed class GetRepositoryHistoryHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetRepositoryHistoryQuery, IReadOnlyList<GitCommitResult>>
{
    public async Task<IReadOnlyList<GitCommitResult>> Handle(GetRepositoryHistoryQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        return await git.GetHistoryAsync(request.ProjectId, request.Take, cancellationToken);
    }
}

public sealed class GetRepositoryDiffHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetRepositoryDiffQuery, GitDiffResult>
{
    public async Task<GitDiffResult> Handle(GetRepositoryDiffQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        return await git.GetDiffAsync(request.ProjectId, request.Staged, cancellationToken);
    }
}

public sealed class GetRepositoryCommitDiffHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetRepositoryCommitDiffQuery, GitDiffResult>
{
    public async Task<GitDiffResult> Handle(GetRepositoryCommitDiffQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        return await git.GetCommitDiffAsync(request.ProjectId, request.Sha, cancellationToken);
    }
}
