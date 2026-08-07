using System;
namespace Coding.Models
{
    public class Workspace : Base
    {

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? LogoUrl { get; set; }

        public Guid OwnerId { get; set; }

        public User Owner { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public ICollection<Project> Projects { get; set; } = [];

        public ICollection<WorkspaceMember> Members { get; set; } = [];
    }
}
