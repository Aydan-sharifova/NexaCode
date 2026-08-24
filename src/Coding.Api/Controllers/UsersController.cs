using Coding.Application.Features.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/users"), EnableRateLimiting("user-search")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("search")]
    public Task<UserSearchPage> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2 || page < 1 || pageSize is < 1 or > 20)
            throw new FluentValidation.ValidationException("Enter at least 2 characters and use a page size between 1 and 20.");
        return sender.Send(new SearchUsersQuery(q, page, pageSize), ct);
    }

    [HttpGet("{publicId}/profile")]
    public Task<PublicUserProfileDto> Profile(string publicId, CancellationToken ct) =>
        sender.Send(new GetPublicUserProfileQuery(publicId), ct);

    [HttpGet("{publicId}/portfolio")]
    public Task<DeveloperPortfolioDto> Portfolio(string publicId,CancellationToken ct)=>sender.Send(new GetDeveloperPortfolioQuery(publicId),ct);

    [HttpPut("profile")]
    public Task<PublicUserProfileDto> UpdateProfile([FromBody] UpdateDeveloperProfileCommand command, CancellationToken ct) =>
        sender.Send(command, ct);

    [HttpPost("{publicId}/follow")]
    public Task<FollowStateDto> Follow(string publicId, CancellationToken ct) =>
        sender.Send(new FollowUserCommand(publicId), ct);

    [HttpDelete("{publicId}/follow")]
    public Task<FollowStateDto> Unfollow(string publicId, CancellationToken ct) =>
        sender.Send(new UnfollowUserCommand(publicId), ct);

    [HttpPost("{publicId}/block")]
    public Task<BlockStateDto> Block(string publicId, CancellationToken ct) =>
        sender.Send(new BlockUserCommand(publicId), ct);

    [HttpDelete("{publicId}/block")]
    public Task<BlockStateDto> Unblock(string publicId, CancellationToken ct) =>
        sender.Send(new UnblockUserCommand(publicId), ct);

    [HttpGet("blocked")]
    public Task<BlockedUserPage> Blocked([FromQuery] string? cursor = null, [FromQuery] int limit = 30, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100) throw new FluentValidation.ValidationException("Use a limit between 1 and 100.");
        return sender.Send(new GetBlockedUsersQuery(cursor, limit), ct);
    }

    [HttpGet("{publicId}/projects/public")]
    public Task<PublicProjectPage> PublicProjects(string publicId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 20) throw new FluentValidation.ValidationException("Invalid pagination.");
        return sender.Send(new GetPublicUserProjectsQuery(publicId, page, pageSize), ct);
    }
}
