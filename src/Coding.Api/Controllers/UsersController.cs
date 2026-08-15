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

    [HttpGet("{publicId}/projects/public")]
    public Task<PublicProjectPage> PublicProjects(string publicId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 20) throw new FluentValidation.ValidationException("Invalid pagination.");
        return sender.Send(new GetPublicUserProjectsQuery(publicId, page, pageSize), ct);
    }
}
