using Coding.Application.Abstractions;
using Coding.Application.Features.DatabaseMetadata;
using Coding.Data;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Coding.Infrastructure.DatabaseMetadata;

public sealed class GetProjectDatabaseSchemaHandler(AppDbContext db, ICurrentUser user)
    : IRequestHandler<GetProjectDatabaseSchemaQuery, ProjectDatabaseDto>
{
    public async Task<ProjectDatabaseDto> Handle(GetProjectDatabaseSchemaQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        var project = await db.Projects.AsNoTracking().Where(item => item.ID == request.ProjectId)
            .Select(item => new { item.DatabaseProvider, item.DatabaseSchemaJson }).SingleAsync(cancellationToken);
        var schemas = string.IsNullOrWhiteSpace(project.DatabaseSchemaJson)
            ? []
            : JsonSerializer.Deserialize<List<DatabaseSchemaDto>>(project.DatabaseSchemaJson) ?? [];
        var version = await db.Projects.AsNoTracking().Where(item => item.ID == request.ProjectId).Select(item => item.DatabaseSchemaVersion).SingleAsync(cancellationToken);
        return new(!string.IsNullOrWhiteSpace(project.DatabaseProvider), project.DatabaseProvider, version, schemas);
    }
}

public sealed class ConfigureProjectDatabaseHandler(AppDbContext db, ICurrentUser user)
    : IRequestHandler<ConfigureProjectDatabaseCommand, ProjectDatabaseDto>
{
    private static readonly HashSet<string> Providers = new(StringComparer.OrdinalIgnoreCase) { "PostgreSQL", "MySQL", "SQLServer", "SQLite" };

    public async Task<ProjectDatabaseDto> Handle(ConfigureProjectDatabaseCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        ProjectAccess.RequireManager(role);
        var provider = Providers.FirstOrDefault(item => string.Equals(item, request.Provider?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Select PostgreSQL, MySQL, SQLServer, or SQLite.");
        var schemaName = NormalizeSchemaName(request.SchemaName, provider);
        var project = await db.Projects.SingleAsync(item => item.ID == request.ProjectId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(project.DatabaseProvider))
            throw new ConflictException("This workspace database is already configured.");
        var schemas = CreateStarterSchema(provider, schemaName);
        project.DatabaseProvider = provider;
        project.DatabaseSchemaJson = JsonSerializer.Serialize(schemas);
        project.DatabaseSchemaVersion = 1;
        project.UpdateAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new(true, provider, project.DatabaseSchemaVersion, schemas);
    }

    private static string NormalizeSchemaName(string value, string provider)
    {
        var fallback = provider switch { "PostgreSQL" => "public", "SQLServer" => "dbo", "SQLite" => "main", _ => "app" };
        var result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (result.Length > 63 || !System.Text.RegularExpressions.Regex.IsMatch(result, "^[A-Za-z][A-Za-z0-9_]*$"))
            throw new ArgumentException("Schema name must start with a letter and contain only letters, numbers, or underscores.");
        return result;
    }

    private static List<DatabaseSchemaDto> CreateStarterSchema(string provider, string schema)
    {
        var id = provider switch { "PostgreSQL" => "uuid", "MySQL" => "char(36)", "SQLServer" => "uniqueidentifier", _ => "text" };
        var text = provider switch { "SQLServer" => "nvarchar(255)", _ => "varchar(255)" };
        var timestamp = provider switch { "PostgreSQL" => "timestamp with time zone", "MySQL" => "datetime(6)", "SQLServer" => "datetimeoffset", _ => "text" };
        var defaultId = provider switch { "PostgreSQL" => "gen_random_uuid()", "MySQL" => "UUID()", "SQLServer" => "NEWID()", _ => null };
        var columns = new List<DatabaseColumnDto>
        {
            new("id", id, false, true, true, defaultId),
            new("email", text, false, false, true, null),
            new("display_name", text, false, false, false, null),
            new("created_at", timestamp, false, false, false, provider == "SQLite" ? "CURRENT_TIMESTAMP" : "CURRENT_TIMESTAMP")
        };
        var table = new DatabaseTableDto(schema, "users", columns, [], [new("ux_users_email", true, ["email"])]);
        return [new(schema, [table])];
    }
}
