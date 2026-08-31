using System.Text.Json;
using System.Text.RegularExpressions;
using Coding.Application.Abstractions;
using Coding.Application.Features.DatabaseMetadata;
using Coding.Data;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.DatabaseMetadata;

public sealed class GetDatabaseMigrationsHandler(AppDbContext db, ICurrentUser user)
    : IRequestHandler<GetDatabaseMigrationsQuery, IReadOnlyList<DatabaseMigrationDto>>
{
    public async Task<IReadOnlyList<DatabaseMigrationDto>> Handle(GetDatabaseMigrationsQuery request, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, ct);
        return await db.ProjectDatabaseMigrations.AsNoTracking().Where(x => x.ProjectId == request.ProjectId)
            .OrderByDescending(x => x.CreatAt).Select(x => new DatabaseMigrationDto(x.ID, x.Name, x.BaseVersion, x.Status.ToString(), x.DdlPreview, x.CreatAt, x.AppliedAt)).ToListAsync(ct);
    }
}

public sealed class CreateTableMigrationHandler(AppDbContext db, ICurrentUser user)
    : IRequestHandler<CreateTableMigrationCommand, DatabaseMigrationDto>
{
    private static readonly Regex Identifier = new("^[A-Za-z][A-Za-z0-9_]{0,62}$", RegexOptions.Compiled);
    private static readonly HashSet<string> LogicalTypes = new(StringComparer.OrdinalIgnoreCase) { "uuid", "string", "text", "integer", "boolean", "decimal", "timestamp" };

    public async Task<DatabaseMigrationDto> Handle(CreateTableMigrationCommand request, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, ct);
        ProjectAccess.RequireManager(role);
        var project = await db.Projects.SingleAsync(x => x.ID == request.ProjectId, ct);
        if (string.IsNullOrWhiteSpace(project.DatabaseProvider)) throw new ConflictException("Configure the workspace database first.");
        if (project.DatabaseSchemaVersion != request.ExpectedVersion) throw new ConflictException("The schema changed. Refresh before creating a migration.");
        var name = RequiredIdentifier(request.Name, "Migration name");
        var schemaName = RequiredIdentifier(request.Schema, "Schema name");
        var tableName = RequiredIdentifier(request.Table, "Table name");
        if (request.Columns is null || request.Columns.Count is < 1 or > 64) throw new ArgumentException("A table must contain between 1 and 64 columns.");
        var columns = request.Columns.Select(x => new CreateTableColumnRequest(RequiredIdentifier(x.Name, "Column name"), NormalizeType(x.Type), x.IsNullable, x.IsUnique)).ToList();
        if (columns.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count) throw new ArgumentException("Column names must be unique.");

        var schemas = JsonSerializer.Deserialize<List<DatabaseSchemaDto>>(project.DatabaseSchemaJson ?? "[]") ?? [];
        var schema = schemas.SingleOrDefault(x => x.Name.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Select an existing schema.");
        if (schema.Tables.Any(x => x.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))) throw new ConflictException("A table with this name already exists.");
        var mapped = columns.Select(x => new DatabaseColumnDto(x.Name, MapType(project.DatabaseProvider!, x.Type), x.IsNullable, false, x.IsUnique, null)).ToList();
        var indexes = columns.Where(x => x.IsUnique).Select(x => new DatabaseIndexDto($"ux_{tableName}_{x.Name}", true, [x.Name])).ToList();
        var table = new DatabaseTableDto(schema.Name, tableName, mapped, [], indexes);
        var proposed = schemas.Select(x => x.Name == schema.Name ? new DatabaseSchemaDto(x.Name, [.. x.Tables, table]) : x).ToList();
        var ddl = BuildDdl(project.DatabaseProvider!, schemaName, tableName, mapped);
        var migration = new ProjectDatabaseMigration { ID = Guid.NewGuid(), ProjectId = request.ProjectId, CreatedById = user.UserId, Name = name, BaseVersion = project.DatabaseSchemaVersion, Status = ProjectDatabaseMigrationStatus.Draft, ProposedSchemaJson = JsonSerializer.Serialize(proposed), DdlPreview = ddl, CreatAt = DateTime.UtcNow };
        db.ProjectDatabaseMigrations.Add(migration);
        await db.SaveChangesAsync(ct);
        return ToDto(migration);
    }

    private static string RequiredIdentifier(string? value, string label) { var result = value?.Trim() ?? ""; if (!Identifier.IsMatch(result)) throw new ArgumentException($"{label} must start with a letter and contain only letters, numbers, or underscores."); return result; }
    private static string NormalizeType(string? value) => LogicalTypes.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Unsupported column type.");
    private static string MapType(string provider, string type) => (provider, type.ToLowerInvariant()) switch { (_, "uuid") => provider switch { "PostgreSQL" => "uuid", "SQLServer" => "uniqueidentifier", "MySQL" => "char(36)", _ => "text" }, (_, "string") => provider == "SQLServer" ? "nvarchar(255)" : "varchar(255)", (_, "text") => provider == "SQLServer" ? "nvarchar(max)" : "text", (_, "integer") => "integer", (_, "boolean") => provider == "SQLServer" ? "bit" : provider == "SQLite" ? "integer" : "boolean", (_, "decimal") => "decimal(18,2)", (_, "timestamp") => provider switch { "PostgreSQL" => "timestamp with time zone", "MySQL" => "datetime(6)", "SQLServer" => "datetimeoffset", _ => "text" }, _ => throw new ArgumentException("Unsupported column type.") };
    private static string BuildDdl(string provider, string schema, string table, IReadOnlyList<DatabaseColumnDto> columns) { string Q(string value) => provider switch { "MySQL" => $"`{value}`", "SQLServer" => $"[{value}]", _ => $"\"{value}\"" }; var definitions = columns.Select(x => $"  {Q(x.Name)} {x.DataType}{(x.IsNullable ? "" : " NOT NULL")}{(x.IsUnique ? " UNIQUE" : "")}"); return $"CREATE TABLE {Q(schema)}.{Q(table)} (\n{string.Join(",\n", definitions)}\n);"; }
    internal static DatabaseMigrationDto ToDto(ProjectDatabaseMigration x) => new(x.ID, x.Name, x.BaseVersion, x.Status.ToString(), x.DdlPreview, x.CreatAt, x.AppliedAt);
}

public sealed class ApplyDatabaseMigrationHandler(AppDbContext db, ICurrentUser user)
    : IRequestHandler<ApplyDatabaseMigrationCommand, ProjectDatabaseDto>
{
    public async Task<ProjectDatabaseDto> Handle(ApplyDatabaseMigrationCommand request, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, ct);
        ProjectAccess.RequireManager(role);
        if (!request.Confirm) throw new ConflictException("Explicit migration confirmation is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var project = await db.Projects.SingleAsync(x => x.ID == request.ProjectId, ct);
        var migration = await db.ProjectDatabaseMigrations.SingleOrDefaultAsync(x => x.ID == request.MigrationId && x.ProjectId == request.ProjectId, ct) ?? throw new NotFoundException("Database migration not found.");
        if (migration.Status != ProjectDatabaseMigrationStatus.Draft) throw new ConflictException("Only a draft migration can be applied.");
        if (project.DatabaseSchemaVersion != request.ExpectedVersion || migration.BaseVersion != request.ExpectedVersion) throw new ConflictException("The schema changed. Review a fresh migration preview.");
        project.DatabaseSchemaJson = migration.ProposedSchemaJson;
        project.DatabaseSchemaVersion++;
        project.UpdateAt = DateTime.UtcNow;
        migration.Status = ProjectDatabaseMigrationStatus.Applied;
        migration.AppliedAt = DateTime.UtcNow;
        migration.UpdateAt = migration.AppliedAt;
        var stale = await db.ProjectDatabaseMigrations.Where(x => x.ProjectId == request.ProjectId && x.Status == ProjectDatabaseMigrationStatus.Draft && x.ID != migration.ID).ToListAsync(ct);
        foreach (var item in stale) { item.Status = ProjectDatabaseMigrationStatus.Superseded; item.UpdateAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var schemas = JsonSerializer.Deserialize<List<DatabaseSchemaDto>>(project.DatabaseSchemaJson) ?? [];
        return new(true, project.DatabaseProvider, project.DatabaseSchemaVersion, schemas);
    }
}
