using Coding.Application.Features.KnowledgeGraph;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/knowledge-graph")]
public sealed class KnowledgeGraphController(ISender sender) : ControllerBase
{
    [HttpPost("index"), EnableRateLimiting("ai")]
    public Task<KnowledgeGraphDto> Index(Guid projectId, CancellationToken ct) => sender.Send(new IndexKnowledgeGraphCommand(projectId), ct);
    [HttpGet]
    public Task<KnowledgeGraphDto> Get(Guid projectId, CancellationToken ct) => sender.Send(new GetKnowledgeGraphQuery(projectId), ct);
    [HttpGet("nodes/{nodeId:guid}/impact")]
    public Task<ImpactAnalysisDto> Impact(Guid projectId, Guid nodeId, CancellationToken ct) => sender.Send(new GetImpactAnalysisQuery(projectId, nodeId), ct);
    [HttpPost("nodes/{nodeId:guid}/impact-report"), EnableRateLimiting("ai")]
    public Task<ImpactAnalysisDto> Report(Guid projectId, Guid nodeId, CancellationToken ct) => sender.Send(new GenerateImpactReportCommand(projectId, nodeId), ct);
}
