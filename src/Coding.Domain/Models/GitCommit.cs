using System;
namespace Coding.Models
{
    public class GitCommit:Base
    {
        public Guid ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public string CommitMessage { get; set; } = string.Empty;

        public string CommitHash { get; set; } = string.Empty;

        public DateTime CommitDate { get; set; }
    }
}
