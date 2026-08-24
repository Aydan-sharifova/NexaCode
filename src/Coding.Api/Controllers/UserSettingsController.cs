using Coding.Application.Features.UserSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/settings")]
public sealed class UserSettingsController(ISender sender) : ControllerBase
{
    [HttpGet] public Task<UserSettingsDto> Get(CancellationToken ct) => sender.Send(new GetUserSettingsQuery(), ct);
    [HttpPut("profile")] public Task<UserProfileDto> Profile(UpdateProfileCommand command, CancellationToken ct) => sender.Send(command, ct);
    [HttpPut("preferences")] public Task<UserPreferenceDto> Preferences(UpdatePreferencesCommand command, CancellationToken ct) => sender.Send(command, ct);
    [HttpPut("notifications")] public async Task<IActionResult> Notifications(UpdateNotificationPreferencesCommand command, CancellationToken ct) { await sender.Send(command, ct); return NoContent(); }
    [HttpPut("password")] public async Task<IActionResult> Password(ChangePasswordCommand command, CancellationToken ct) { await sender.Send(command, ct); return NoContent(); }
    [HttpPost("avatar")]
    [RequestSizeLimit(5_242_880)]
    [EnableRateLimiting("uploads")]
    public async Task<ActionResult<object>> Avatar(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream(); using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct);
        var url = await sender.Send(new UploadAvatarCommand(memory.ToArray(), file.ContentType, Path.GetExtension(file.FileName)), ct);
        return Ok(new { url });
    }
    [HttpDelete("avatar")] public async Task<IActionResult> RemoveAvatar(CancellationToken ct) { await sender.Send(new RemoveAvatarCommand(), ct); return NoContent(); }
    [HttpGet("sessions")] public Task<IReadOnlyList<UserSessionDto>> Sessions(CancellationToken ct) => sender.Send(new GetUserSessionsQuery(), ct);
    [HttpDelete("sessions/{sessionId:guid}")] public async Task<IActionResult> Revoke(Guid sessionId, CancellationToken ct) { await sender.Send(new RevokeUserSessionCommand(sessionId), ct); return NoContent(); }
}
