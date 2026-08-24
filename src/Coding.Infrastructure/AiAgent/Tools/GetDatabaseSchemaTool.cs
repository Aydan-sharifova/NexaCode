using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns database schema metadata derived from the EF Core model snapshot.
/// Never executes SQL. Never returns connection strings or data.
/// </summary>
public sealed class GetDatabaseSchemaTool(AppDbContext db) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "get_database_schema",
        Description: "Returns authorized database schema metadata for the application database. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(GetDatabaseSchemaInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);

        var tables = new List<object>();
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName() ?? entityType.GetType().Name;
            var columns = entityType.GetProperties().Select(p => new
            {
                name = p.GetColumnName(),
                type = p.GetColumnType(),
                nullable = p.IsNullable,
                isPrimaryKey = p.IsPrimaryKey(),
                isForeignKey = p.IsForeignKey(),
                maxLength = p.GetMaxLength()
            }).ToArray();
            var fks = entityType.GetForeignKeys().Select(fk => new
            {
                principalTable = fk.PrincipalEntityType.GetTableName(),
                columns = fk.Properties.Select(p => p.GetColumnName()).ToArray(),
                principalColumns = fk.PrincipalKey.Properties.Select(p => p.GetColumnName()).ToArray()
            }).ToArray();
            var indexes = entityType.GetIndexes().Select(ix => new
            {
                name = ix.GetDatabaseName(),
                isUnique = ix.IsUnique,
                columns = ix.Properties.Select(p => p.GetColumnName()).ToArray()
            }).ToArray();
            tables.Add(new { table = tableName, columns, foreignKeys = fks, indexes });
        }

        var json = JsonSerializer.Serialize(new { database = "application", tables });
        return new AiReadToolGuard.AiTextResult($"{tables.Count} tables", json);
    }
}

public sealed record GetDatabaseSchemaInput();
