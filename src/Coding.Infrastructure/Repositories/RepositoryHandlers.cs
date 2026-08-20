using System.Text;
using Coding.Application.Abstractions;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
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
        var contents = await db.FileContents.AsNoTracking()
            .Where(content => content.Node.ProjectId == projectId)
            .Select(content => new { content.NodeId, content.Content })
            .ToDictionaryAsync(content => content.NodeId, ct);

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
                await git.WriteFileAsync(projectId, string.Join('/', parts), Encoding.UTF8.GetBytes(content.Content), ct);
        }
    }
}

public sealed class GetRepositoryStatusHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<GetRepositoryStatusQuery, GitStatusResult>
{
    public async Task<GitStatusResult> Handle(GetRepositoryStatusQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await RepositoryMaterializer.SynchronizeAsync(db, git, request.ProjectId, cancellationToken);
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

public sealed class CreateRepositoryBranchHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<CreateRepositoryBranchCommand>
{
    public async Task Handle(CreateRepositoryBranchCommand request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.InitializeAsync(request.ProjectId, "main", cancellationToken);
        await git.CreateBranchAsync(request.ProjectId, request.Name, cancellationToken);
    }
}

public sealed class CheckoutRepositoryBranchHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<CheckoutRepositoryBranchCommand>
{
    public async Task Handle(CheckoutRepositoryBranchCommand request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await git.CheckoutAsync(request.ProjectId, request.Name, cancellationToken);
    }
}

public sealed class CommitRepositoryChangesHandler(AppDbContext db, ICurrentUser user, IGitRepositoryService git) : IRequestHandler<CommitRepositoryChangesCommand, GitCommitResult>
{
    public async Task<GitCommitResult> Handle(CommitRepositoryChangesCommand request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        await RepositoryMaterializer.SynchronizeAsync(db, git, request.ProjectId, cancellationToken);
        var author = await db.Users.AsNoTracking().Where(item => item.ID == user.UserId)
            .Select(item => new { Name = item.FirstName + " " + item.LastName, item.Email })
            .SingleAsync(cancellationToken);
        return await git.CommitAllAsync(request.ProjectId, request.Message, author.Name, author.Email, cancellationToken);
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
