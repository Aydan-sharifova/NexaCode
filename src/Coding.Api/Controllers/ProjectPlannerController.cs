using Coding.Application.Features.ProjectPlanner;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/project-plans")]
public sealed class ProjectPlannerController(ISender sender) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<ProjectPlanSummary>> List(CancellationToken ct) => sender.Send(new ListProjectPlansQuery(), ct);
    [HttpGet("{planId:guid}")] public Task<ProjectPlanDetails> Get(Guid planId, CancellationToken ct) => sender.Send(new GetProjectPlanQuery(planId), ct);
    [HttpPost, EnableRateLimiting("ai")]
    public async Task<ActionResult<ProjectPlanDetails>> Generate(GenerateProjectPlanCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { planId = result.Id }, result);
    }
    [HttpPost("{planId:guid}/approve")] public Task<ProjectPlanDetails> Approve(Guid planId, PlanVersionRequest request, CancellationToken ct) => sender.Send(new ApproveProjectPlanCommand(planId, request.ExpectedVersion), ct);
    [HttpPost("{planId:guid}/reject")] public Task<ProjectPlanDetails> Reject(Guid planId, PlanVersionRequest request, CancellationToken ct) => sender.Send(new RejectProjectPlanCommand(planId, request.ExpectedVersion), ct);
    [HttpPost("{planId:guid}/apply")] public Task<Guid> Apply(Guid planId, ApplyPlanRequest request, CancellationToken ct) => sender.Send(new ApplyProjectPlanCommand(planId, request.ExpectedVersion, request.ConfirmBulkCreation), ct);
}
public sealed record PlanVersionRequest(int ExpectedVersion);
public sealed record ApplyPlanRequest(int ExpectedVersion, bool ConfirmBulkCreation);
