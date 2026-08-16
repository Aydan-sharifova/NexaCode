using MediatR;

namespace Coding.Application.Features.Repositories;

public sealed record GitFileStatus(string Path, string IndexStatus, string WorkingTreeStatus);
public sealed record GitStatusResult(string CurrentBranch, bool IsClean, IReadOnlyList<GitFileStatus> Files);
public sealed record GitBranchResult(string Name, bool IsCurrent);
public sealed record GitCommitResult(string Sha, string ShortSha, string AuthorName, string AuthorEmail, string Message, DateTimeOffset CommittedAt);
public sealed record GitDiffResult(string Patch);

public interface IGitRepositoryService
{
    Task InitializeAsync(Guid projectId, string defaultBranch, CancellationToken cancellationToken = default);
    Task<GitStatusResult> GetStatusAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitBranchResult>> GetBranchesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task CreateBranchAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default);
    Task CheckoutAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default);
    Task<GitCommitResult> CommitAllAsync(Guid projectId, string message, string authorName, string authorEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitCommitResult>> GetHistoryAsync(Guid projectId, int take, CancellationToken cancellationToken = default);
    Task<GitDiffResult> GetDiffAsync(Guid projectId, bool staged, CancellationToken cancellationToken = default);
    Task WriteFileAsync(Guid projectId, string projectPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<byte[]> ReadFileAsync(Guid projectId, string projectPath, CancellationToken cancellationToken = default);
}

public sealed record GetRepositoryStatusQuery(Guid ProjectId) : IRequest<GitStatusResult>;
public sealed record GetRepositoryBranchesQuery(Guid ProjectId) : IRequest<IReadOnlyList<GitBranchResult>>;
public sealed record CreateRepositoryBranchCommand(Guid ProjectId, string Name) : IRequest;
public sealed record CheckoutRepositoryBranchCommand(Guid ProjectId, string Name) : IRequest;
public sealed record CommitRepositoryChangesCommand(Guid ProjectId, string Message) : IRequest<GitCommitResult>;
public sealed record GetRepositoryHistoryQuery(Guid ProjectId, int Take = 30) : IRequest<IReadOnlyList<GitCommitResult>>;
public sealed record GetRepositoryDiffQuery(Guid ProjectId, bool Staged = false) : IRequest<GitDiffResult>;
