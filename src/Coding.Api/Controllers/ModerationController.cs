using Coding.Application.Features.Moderation;
using Coding.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, EnableRateLimiting("social"), Route("api/reports")]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpPost] public async Task<ActionResult<ContentReportItem>> Create(CreateContentReportRequest request, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new CreateContentReportCommand(request.TargetType, request.TargetId, request.Reason, request.Details), ct));
    [HttpGet("mine")] public Task<ModerationQueue> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) => sender.Send(new GetMyContentReportsQuery(page, pageSize), ct);
}

[ApiController, Authorize(Roles = "SuperAdmin,Admin,Moderator"), EnableRateLimiting("social"), Route("api/moderation/reports")]
public sealed class ModerationController(ISender sender) : ControllerBase
{
    [HttpGet] public Task<ModerationQueue> Queue([FromQuery] ModerationReportState? state, [FromQuery] ReportTargetType? targetType, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) => sender.Send(new GetModerationQueueQuery(state, targetType, page, pageSize), ct);
    [HttpPost("{reportId:guid}/actions")] public Task<ContentReportItem> Act(Guid reportId, ModerateContentReportRequest request, CancellationToken ct) => sender.Send(new ModerateContentReportCommand(reportId, request.Action, request.Note), ct);
}

public sealed record CreateContentReportRequest(ReportTargetType TargetType, Guid TargetId, string Reason, string? Details);
public sealed record ModerateContentReportRequest(ModerationActionType Action, string Note);
