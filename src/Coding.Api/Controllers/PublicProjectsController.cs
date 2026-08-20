using Coding.Application.Features.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/public-projects")]
public sealed class PublicProjectsController(ISender sender) : ControllerBase
{
    [HttpGet("{projectId:guid}")]
    public Task<PublicProjectDetailsDto> Details(Guid projectId, CancellationToken ct) =>
        sender.Send(new GetPublicProjectDetailsQuery(projectId), ct);

    [HttpGet("{projectId:guid}/tree")]
    public Task<IReadOnlyList<PublicProjectNodeDto>> Tree(Guid projectId, CancellationToken ct) =>
        sender.Send(new GetPublicProjectTreeQuery(projectId), ct);

    [HttpGet("{projectId:guid}/files/{nodeId:guid}")]
    public Task<PublicProjectFileDto> File(Guid projectId, Guid nodeId, CancellationToken ct) =>
        sender.Send(new GetPublicProjectFileQuery(projectId, nodeId), ct);
}
