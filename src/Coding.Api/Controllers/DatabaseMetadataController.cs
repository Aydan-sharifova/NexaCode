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

    [HttpGet("migrations")]
    public Task<IReadOnlyList<DatabaseMigrationDto>> GetMigrations(Guid projectId, CancellationToken cancellationToken) =>
        sender.Send(new GetDatabaseMigrationsQuery(projectId), cancellationToken);

    [HttpPost("migrations/tables")]
    public Task<DatabaseMigrationDto> CreateTableMigration(Guid projectId, CreateTableMigrationRequest request, CancellationToken cancellationToken) =>
        sender.Send(new CreateTableMigrationCommand(projectId, request.Name, request.Schema, request.Table, request.Columns, request.ExpectedVersion), cancellationToken);

    [HttpPost("migrations/{migrationId:guid}/apply")]
    public Task<ProjectDatabaseDto> ApplyMigration(Guid projectId, Guid migrationId, ApplyDatabaseMigrationRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ApplyDatabaseMigrationCommand(projectId, migrationId, request.ExpectedVersion, request.Confirm), cancellationToken);
}
