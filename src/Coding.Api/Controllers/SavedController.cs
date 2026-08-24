using Coding.Application.Features.Saved;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController,Authorize,EnableRateLimiting("social"),Route("api/saved")]
public sealed class SavedController(ISender sender):ControllerBase
{
    [HttpGet] public Task<SavedContent> List([FromQuery]SavedContentType type=SavedContentType.All,[FromQuery]string? search=null,[FromQuery]int limit=50,CancellationToken ct=default)=>sender.Send(new GetSavedContentQuery(type,search,limit),ct);
    [HttpPut("projects/{projectId:guid}")] public Task<bool> Project(Guid projectId,ToggleSavedProjectRequest request,CancellationToken ct)=>sender.Send(new SetProjectSavedCommand(projectId,request.Saved),ct);
}
public sealed record ToggleSavedProjectRequest(bool Saved);
