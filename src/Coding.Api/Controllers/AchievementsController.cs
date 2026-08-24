using Coding.Application.Features.Achievements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/achievements")]
public sealed class AchievementsController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    public Task<DeveloperAchievementProfile> Mine(CancellationToken ct) => sender.Send(new GetMyAchievementsQuery(), ct);
    [HttpGet("users/{publicId}")]
    public Task<DeveloperAchievementProfile> UserAchievements(string publicId, CancellationToken ct) => sender.Send(new GetUserAchievementsQuery(publicId), ct);
    [HttpGet("users/{publicId}/journey")]
    public Task<IReadOnlyList<DeveloperJourneyItem>> Journey(string publicId, CancellationToken ct) => sender.Send(new GetDeveloperJourneyQuery(publicId), ct);
}
