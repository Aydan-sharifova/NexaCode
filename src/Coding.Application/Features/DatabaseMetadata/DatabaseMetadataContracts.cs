using MediatR;

namespace Coding.Application.Features.DatabaseMetadata;

public sealed record DatabaseColumnDto(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, bool IsUnique, string? DefaultValue);
public sealed record DatabaseForeignKeyDto(string Name, string SourceTable, IReadOnlyList<string> SourceColumns, string TargetTable, IReadOnlyList<string> TargetColumns);
public sealed record DatabaseIndexDto(string Name, bool IsUnique, IReadOnlyList<string> Columns);
public sealed record DatabaseTableDto(string Schema, string Name, IReadOnlyList<DatabaseColumnDto> Columns, IReadOnlyList<DatabaseForeignKeyDto> ForeignKeys, IReadOnlyList<DatabaseIndexDto> Indexes);
public sealed record DatabaseSchemaDto(string Name, IReadOnlyList<DatabaseTableDto> Tables);
public sealed record ProjectDatabaseDto(bool IsConfigured, string? Provider, int Version, IReadOnlyList<DatabaseSchemaDto> Schemas);
public sealed record ConfigureProjectDatabaseRequest(string Provider, string SchemaName);
public sealed record CreateTableColumnRequest(string Name, string Type, bool IsNullable = true, bool IsUnique = false);
public sealed record CreateTableMigrationRequest(string Name, string Schema, string Table, IReadOnlyList<CreateTableColumnRequest> Columns, int ExpectedVersion);
public sealed record ApplyDatabaseMigrationRequest(int ExpectedVersion, bool Confirm);
public sealed record DatabaseMigrationDto(Guid Id, string Name, int BaseVersion, string Status, string DdlPreview, DateTime CreatedAt, DateTime? AppliedAt);

public sealed record GetProjectDatabaseSchemaQuery(Guid ProjectId) : IRequest<ProjectDatabaseDto>;
public sealed record ConfigureProjectDatabaseCommand(Guid ProjectId, string Provider, string SchemaName) : IRequest<ProjectDatabaseDto>;
public sealed record GetDatabaseMigrationsQuery(Guid ProjectId) : IRequest<IReadOnlyList<DatabaseMigrationDto>>;
public sealed record CreateTableMigrationCommand(Guid ProjectId, string Name, string Schema, string Table, IReadOnlyList<CreateTableColumnRequest> Columns, int ExpectedVersion) : IRequest<DatabaseMigrationDto>;
public sealed record ApplyDatabaseMigrationCommand(Guid ProjectId, Guid MigrationId, int ExpectedVersion, bool Confirm) : IRequest<ProjectDatabaseDto>;

public interface IDatabaseMetadataProvider
{
    Task<IReadOnlyList<DatabaseSchemaDto>> GetSchemaAsync(CancellationToken cancellationToken);
}
