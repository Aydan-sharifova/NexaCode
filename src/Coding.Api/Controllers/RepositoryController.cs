using Coding.Application.Features.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/repository")]
public sealed class RepositoryController(ISender sender) : ControllerBase
{
    [HttpGet("status")]
    public Task<GitStatusResult> Status(Guid projectId, CancellationToken cancellationToken) => sender.Send(new GetRepositoryStatusQuery(projectId), cancellationToken);

    [HttpGet("branches")]
    public Task<IReadOnlyList<GitBranchResult>> Branches(Guid projectId, CancellationToken cancellationToken) => sender.Send(new GetRepositoryBranchesQuery(projectId), cancellationToken);

    [HttpPost("branches")]
    public async Task<IActionResult> CreateBranch(Guid projectId, BranchRequest request, CancellationToken cancellationToken)
    { await sender.Send(new CreateRepositoryBranchCommand(projectId, request.Name), cancellationToken); return NoContent(); }

    [HttpPost("branches/checkout")]
    public async Task<IActionResult> Checkout(Guid projectId, BranchRequest request, CancellationToken cancellationToken)
    { await sender.Send(new CheckoutRepositoryBranchCommand(projectId, request.Name), cancellationToken); return NoContent(); }

    [HttpPost("commits")]
    public Task<GitCommitResult> Commit(Guid projectId, CommitRequest request, CancellationToken cancellationToken) => sender.Send(new CommitRepositoryChangesCommand(projectId, request.Message), cancellationToken);

    [HttpPost("stage")]
    public async Task<IActionResult> Stage(Guid projectId, RepositoryPathRequest request, CancellationToken cancellationToken)
    { await sender.Send(new StageRepositoryFileCommand(projectId, request.Path), cancellationToken); return NoContent(); }

    [HttpPost("unstage")]
    public async Task<IActionResult> Unstage(Guid projectId, RepositoryPathRequest request, CancellationToken cancellationToken)
    { await sender.Send(new UnstageRepositoryFileCommand(projectId, request.Path), cancellationToken); return NoContent(); }

    [HttpGet("commits")]
    public Task<IReadOnlyList<GitCommitResult>> History(Guid projectId, [FromQuery] int take = 30, CancellationToken cancellationToken = default) => sender.Send(new GetRepositoryHistoryQuery(projectId, take), cancellationToken);

    [HttpGet("commits/{sha}/diff")]
    public Task<GitDiffResult> CommitDiff(Guid projectId, string sha, CancellationToken cancellationToken) => sender.Send(new GetRepositoryCommitDiffQuery(projectId, sha), cancellationToken);

    [HttpGet("diff")]
    public Task<GitDiffResult> Diff(Guid projectId, [FromQuery] bool staged = false, CancellationToken cancellationToken = default) => sender.Send(new GetRepositoryDiffQuery(projectId, staged), cancellationToken);
}

public sealed record BranchRequest(string Name);
public sealed record CommitRequest(string Message);
public sealed record RepositoryPathRequest(string Path);
