using Coding.Application.Features.Mentor;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/mentor")]
public sealed class MentorController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<MentorAnalysis> Get(CancellationToken cancellationToken) => sender.Send(new GetMentorAnalysisQuery(), cancellationToken);

    [HttpPost("generate"), EnableRateLimiting("ai")]
    public Task<MentorAnalysis> Generate(CancellationToken cancellationToken) => sender.Send(new GenerateMentorAnalysisCommand(), cancellationToken);
}
