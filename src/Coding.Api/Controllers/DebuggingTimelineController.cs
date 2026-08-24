using Coding.Application.Features.Debugging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/debugging")]
public sealed class DebuggingTimelineController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<DebuggingTimelineDto> List(Guid projectId, [FromQuery] int take = 30, CancellationToken ct = default) => sender.Send(new ListDebuggingTimelineQuery(projectId, take), ct);

    [HttpGet("{incidentId:guid}")]
    public Task<DebuggingIncidentDto> Get(Guid projectId, Guid incidentId, CancellationToken ct) => sender.Send(new GetDebuggingIncidentQuery(projectId, incidentId), ct);

    [HttpPost("{incidentId:guid}/analyze"), EnableRateLimiting("ai")]
    public Task<DebuggingIncidentDto> Analyze(Guid projectId, Guid incidentId, AnalyzeDebuggingRequest request, CancellationToken ct) => sender.Send(new AnalyzeDebuggingIncidentCommand(projectId, incidentId, request.UseModel), ct);
}

public sealed record AnalyzeDebuggingRequest(bool UseModel = true);
