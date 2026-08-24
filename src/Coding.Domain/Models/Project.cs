using System;
using Coding.Enums;
namespace Coding.Models
{
    public class Project:Base
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid? WorkspaceId { get; set; }

        public Workspace? Workspace { get; set; }

        public Guid OwnerId { get; set; }

        public User Owner { get; set; } = null!;

        public string DefaultLanguage { get; set; } = string.Empty;

        public bool IsPublic { get; set; }

        public string? DatabaseProvider { get; set; }

        public string? DatabaseSchemaJson { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Active;

        public string ProtectedBranch { get; set; } = "main";

        public int RequiredPullRequestApprovals { get; set; } = 1;

        public bool RequirePassingPullRequestTests { get; set; }

        public ICollection<ProjectMember> Members { get; set; } = [];

        public ICollection<ProjectInvitation> Invitations { get; set; } = [];

        public ICollection<WorkspaceNode> WorkspaceNodes { get; set; } = [];

        public ICollection<Folder> Folders { get; set; } = [];

        public ICollection<GitCommit> Commits { get; set; } = [];
        public ICollection<ProjectTask> Tasks { get; set; } = [];
        public ICollection<ProjectMilestone> Milestones { get; set; } = [];
        public ICollection<ProjectIssue> Issues { get; set; } = [];
        public ICollection<KnowledgeGraphSnapshot> KnowledgeGraphSnapshots { get; set; } = [];
    public ICollection<DebuggingIncident> DebuggingIncidents { get; set; } = [];
    public ICollection<AutonomousTestRun> AutonomousTestRuns { get; set; } = [];
    public ICollection<ScreenshotCodeGeneration> ScreenshotCodeGenerations { get; set; } = [];
    public ICollection<AiUiGeneration> AiUiGenerations { get; set; } = [];
        public ICollection<PullRequest> PullRequests { get; set; } = [];
        public ICollection<ProjectDeployment> Deployments { get; set; } = [];
    }
}
