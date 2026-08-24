using Coding.Application.Features.PullRequests;
using Coding.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/pull-requests")]
public sealed class PullRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PullRequestListItem>> List(Guid projectId, [FromQuery] PullRequestStatus? status, CancellationToken ct) =>
        sender.Send(new ListPullRequestsQuery(projectId, status), ct);

    [HttpGet("{number:int}")]
    public Task<PullRequestDetails> Get(Guid projectId, int number, CancellationToken ct) => sender.Send(new GetPullRequestQuery(projectId, number), ct);

    [HttpGet("{number:int}/diff")]
    public Task<PullRequestDiff> Diff(Guid projectId, int number, CancellationToken ct) => sender.Send(new GetPullRequestDiffQuery(projectId, number), ct);

    [HttpPost]
    public async Task<ActionResult<PullRequestDetails>> Create(Guid projectId, CreatePullRequestRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreatePullRequestCommand(projectId, request.Title, request.Description, request.SourceBranch, request.TargetBranch), ct);
        return CreatedAtAction(nameof(Get), new { projectId, number = result.PullRequest.Number }, result);
    }

    [HttpPut("{number:int}/review")]
    public Task<PullRequestDetails> Review(Guid projectId, int number, ReviewPullRequestRequest request, CancellationToken ct) =>
        sender.Send(new ReviewPullRequestCommand(projectId, number, request.Decision, request.Body), ct);

    [HttpPost("{number:int}/comments")]
    public async Task<ActionResult<PullRequestCommentItem>> Comment(Guid projectId, int number, AddPullRequestCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(new AddPullRequestCommentCommand(projectId, number, request.Body, request.FilePath, request.LineNumber, request.IsBlocking), ct));

    [HttpPut("{number:int}/comments/{commentId:guid}/resolve")]
    public Task<PullRequestCommentItem> Resolve(Guid projectId, int number, Guid commentId, CancellationToken ct) =>
        sender.Send(new ResolvePullRequestCommentCommand(projectId, number, commentId), ct);

    [HttpPost("{number:int}/refresh")]
    public Task<PullRequestDetails> Refresh(Guid projectId, int number, CancellationToken ct) => sender.Send(new RefreshPullRequestHeadCommand(projectId, number), ct);

    [HttpPut("{number:int}/tests")]
    public Task<PullRequestDetails> Tests(Guid projectId, int number, ReportPullRequestTestsRequest request, CancellationToken ct) =>
        sender.Send(new ReportPullRequestTestsCommand(projectId, number, request.Passed, request.Summary), ct);

    [HttpPost("{number:int}/merge")]
    public Task<PullRequestDetails> Merge(Guid projectId, int number, CancellationToken ct) => sender.Send(new MergePullRequestCommand(projectId, number), ct);

    [HttpPost("{number:int}/close")]
    public Task<PullRequestDetails> Close(Guid projectId, int number, CancellationToken ct) => sender.Send(new ClosePullRequestCommand(projectId, number), ct);

    [HttpGet("policy")]
    public Task<PullRequestPolicy> Policy(Guid projectId, CancellationToken ct) => sender.Send(new GetPullRequestPolicyQuery(projectId), ct);

    [HttpPut("policy")]
    public Task<PullRequestPolicy> Policy(Guid projectId, ConfigurePullRequestPolicyRequest request, CancellationToken ct) =>
        sender.Send(new ConfigurePullRequestPolicyCommand(projectId, request.ProtectedBranch, request.RequiredApprovals, request.RequirePassingTests), ct);
}

public sealed record CreatePullRequestRequest(string Title, string? Description, string SourceBranch, string? TargetBranch);
public sealed record ReviewPullRequestRequest(PullRequestReviewDecision Decision, string? Body);
public sealed record AddPullRequestCommentRequest(string Body, string? FilePath, int? LineNumber, bool IsBlocking);
public sealed record ReportPullRequestTestsRequest(bool Passed, string? Summary);
public sealed record ConfigurePullRequestPolicyRequest(string ProtectedBranch, int RequiredApprovals, bool RequirePassingTests);
