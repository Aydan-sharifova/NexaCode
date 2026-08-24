using Coding.Application.Features.DatabaseMetadata;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/database")]
public sealed class DatabaseMetadataController(ISender sender) : ControllerBase
{
    [HttpGet("schema")]
    public Task<ProjectDatabaseDto> GetSchema(Guid projectId, CancellationToken cancellationToken) =>
        sender.Send(new GetProjectDatabaseSchemaQuery(projectId), cancellationToken);

    [HttpPost("configure")]
    public Task<ProjectDatabaseDto> Configure(Guid projectId, ConfigureProjectDatabaseRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ConfigureProjectDatabaseCommand(projectId, request.Provider, request.SchemaName), cancellationToken);
}
