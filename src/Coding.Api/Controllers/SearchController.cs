using Coding.Application.Features.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, EnableRateLimiting("user-search"), Route("api/search")]
public sealed class SearchController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<GlobalSearchResponse> Search(
        [FromQuery] string query,
        [FromQuery] SearchResultType? type,
        [FromQuery] Guid? projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        CancellationToken ct = default) =>
        sender.Send(new GlobalSearchQuery(query, type, projectId, page, pageSize), ct);
}
