using Coding.Application.Features.DatabaseMetadata;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/database")]
public sealed class DatabaseMetadataController(ISender sender) : ControllerBase
{
    [HttpGet("schema")]
    public Task<IReadOnlyList<DatabaseSchemaDto>> GetSchema(Guid projectId, CancellationToken cancellationToken) =>
        sender.Send(new GetProjectDatabaseSchemaQuery(projectId), cancellationToken);
}
