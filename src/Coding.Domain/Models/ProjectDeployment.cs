namespace Coding.Models;

public sealed class ProjectDeployment : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid DeployedById { get; set; }
    public User DeployedBy { get; set; } = null!;
    public string Slug { get; set; } = string.Empty;
    public int Version { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public string? CommitSha { get; set; }
    public DateTime DeployedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProjectDeploymentFile> Files { get; set; } = [];
}

public sealed class ProjectDeploymentFile
{
    public Guid DeploymentId { get; set; }
    public ProjectDeployment Deployment { get; set; } = null!;
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string ContentHash { get; set; } = string.Empty;
}
