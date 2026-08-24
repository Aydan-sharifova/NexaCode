using Coding.Application.Features.AiUiGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/ui-generations")]
public sealed class AiUiGeneratorController(IAiUiGeneratorService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<AiUiGenerationDto>> List(Guid projectId,[FromQuery]int take=20,CancellationToken ct=default)=>service.ListAsync(projectId,take,ct);
    [HttpGet("{id:guid}")] public Task<AiUiGenerationDto> Get(Guid projectId,Guid id,CancellationToken ct)=>service.GetAsync(projectId,id,ct);
    [HttpPost,EnableRateLimiting("ai")] public Task<AiUiGenerationDto> Generate(Guid projectId,GenerateAiUiRequest request,CancellationToken ct)=>service.GenerateAsync(projectId,request.Prompt,request.IncludeSampleData,ct);
    [HttpPost("{id:guid}/apply")] public Task<AiUiGenerationDto> Apply(Guid projectId,Guid id,ApplyAiUiRequest request,CancellationToken ct)=>service.ApplyAsync(projectId,id,request.Confirm,ct);
}
public sealed record GenerateAiUiRequest(string Prompt,bool IncludeSampleData=false);
public sealed record ApplyAiUiRequest(bool Confirm);
