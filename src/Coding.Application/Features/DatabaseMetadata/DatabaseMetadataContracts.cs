using MediatR;

namespace Coding.Application.Features.DatabaseMetadata;

public sealed record DatabaseColumnDto(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, bool IsUnique, string? DefaultValue);
public sealed record DatabaseForeignKeyDto(string Name, string SourceTable, IReadOnlyList<string> SourceColumns, string TargetTable, IReadOnlyList<string> TargetColumns);
public sealed record DatabaseIndexDto(string Name, bool IsUnique, IReadOnlyList<string> Columns);
public sealed record DatabaseTableDto(string Schema, string Name, IReadOnlyList<DatabaseColumnDto> Columns, IReadOnlyList<DatabaseForeignKeyDto> ForeignKeys, IReadOnlyList<DatabaseIndexDto> Indexes);
public sealed record DatabaseSchemaDto(string Name, IReadOnlyList<DatabaseTableDto> Tables);

public sealed record GetProjectDatabaseSchemaQuery(Guid ProjectId) : IRequest<IReadOnlyList<DatabaseSchemaDto>>;

public interface IDatabaseMetadataProvider
{
    Task<IReadOnlyList<DatabaseSchemaDto>> GetSchemaAsync(CancellationToken cancellationToken);
}
