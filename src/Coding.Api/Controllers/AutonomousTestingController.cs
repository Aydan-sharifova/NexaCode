using Coding.Application.Features.AutonomousTesting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/autonomous-tests")]
public sealed class AutonomousTestingController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<AutonomousTestTimelineDto> List(Guid projectId, [FromQuery] int take = 30, CancellationToken ct = default) =>
        sender.Send(new ListAutonomousTestRunsQuery(projectId, take), ct);

    [HttpGet("{runId:guid}")]
    public Task<AutonomousTestRunDto> Get(Guid projectId, Guid runId, CancellationToken ct) =>
        sender.Send(new GetAutonomousTestRunQuery(projectId, runId), ct);

    [HttpPost, EnableRateLimiting("ai")]
    public Task<AutonomousTestRunDto> Start(Guid projectId, StartAutonomousTestRequest request, CancellationToken ct) =>
        sender.Send(new StartAutonomousTestRunCommand(projectId, request.WorkspaceNodeId, request.Goal, request.MaximumIterations), ct);

    [HttpPost("{runId:guid}/apply")]
    public Task<AutonomousTestRunDto> Apply(Guid projectId, Guid runId, ApplyAutonomousTestFixRequest request, CancellationToken ct) =>
        sender.Send(new ApplyAutonomousTestFixCommand(projectId, runId, request.Confirm), ct);

    [HttpPost("{runId:guid}/run-again"), EnableRateLimiting("ai")]
    public Task<AutonomousTestRunDto> RunAgain(Guid projectId, Guid runId, RunAutonomousTestsAgainRequest request, CancellationToken ct) =>
        sender.Send(new RunAutonomousTestsAgainCommand(projectId, runId, request.MaximumIterations), ct);
}

public sealed record StartAutonomousTestRequest(Guid WorkspaceNodeId, string Goal, int MaximumIterations = 3);
public sealed record ApplyAutonomousTestFixRequest(bool Confirm);
public sealed record RunAutonomousTestsAgainRequest(int MaximumIterations = 3);
