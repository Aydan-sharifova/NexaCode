using Coding.Application.Features.DatabaseMetadata;
using Coding.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Coding.Infrastructure.DatabaseMetadata;

public sealed class EfCoreDatabaseMetadataProvider(AppDbContext db) : IDatabaseMetadataProvider
{
    public Task<IReadOnlyList<DatabaseSchemaDto>> GetSchemaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tables = db.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .GroupBy(entity => (Schema: entity.GetSchema() ?? "public", Table: entity.GetTableName()!))
            .Select(group => MapTable(group.Key.Schema, group.Key.Table, group.First()))
            .OrderBy(table => table.Name)
            .ToList();
        IReadOnlyList<DatabaseSchemaDto> result = tables.GroupBy(table => table.Schema)
            .OrderBy(group => group.Key)
            .Select(group => new DatabaseSchemaDto(group.Key, group.ToList()))
            .ToList();
        return Task.FromResult(result);
    }

    private static DatabaseTableDto MapTable(string schema, string table, IEntityType entity)
    {
        var store = StoreObjectIdentifier.Table(table, schema);
        var primary = entity.FindPrimaryKey()?.Properties.ToHashSet() ?? [];
        var unique = entity.GetIndexes().Where(index => index.IsUnique).SelectMany(index => index.Properties).ToHashSet();
        var columns = entity.GetProperties().Select(property => new DatabaseColumnDto(
            property.GetColumnName(store) ?? property.Name,
            property.GetColumnType() ?? property.ClrType.Name,
            property.IsNullable,
            primary.Contains(property),
            unique.Contains(property),
            property.GetDefaultValueSql())).OrderBy(column => column.Name).ToList();
        var foreignKeys = entity.GetForeignKeys().Select(key => new DatabaseForeignKeyDto(
            key.GetConstraintName() ?? $"FK_{table}_{key.PrincipalEntityType.GetTableName()}",
            table,
            key.Properties.Select(property => property.GetColumnName(store) ?? property.Name).ToList(),
            key.PrincipalEntityType.GetTableName() ?? key.PrincipalEntityType.Name,
            key.PrincipalKey.Properties.Select(property => property.Name).ToList())).ToList();
        var indexes = entity.GetIndexes().Select(index => new DatabaseIndexDto(
            index.GetDatabaseName() ?? $"IX_{table}", index.IsUnique,
            index.Properties.Select(property => property.GetColumnName(store) ?? property.Name).ToList())).ToList();
        return new DatabaseTableDto(schema, table, columns, foreignKeys, indexes);
    }
}
