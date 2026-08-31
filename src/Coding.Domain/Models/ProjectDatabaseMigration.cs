namespace Coding.Models;

public enum ProjectDatabaseMigrationStatus { Draft = 0, Applied = 1, Superseded = 2 }

public sealed class ProjectDatabaseMigration : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int BaseVersion { get; set; }
    public ProjectDatabaseMigrationStatus Status { get; set; }
    public string ProposedSchemaJson { get; set; } = "[]";
    public string DdlPreview { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
}
