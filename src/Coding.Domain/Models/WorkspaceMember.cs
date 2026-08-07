using System;
using Coding.Enums;

namespace Coding.Models
{
    public class WorkspaceMember : Base
    {

        public Guid WorkspaceId { get; set; }

        public Workspace Workspace { get; set; } = null!;

        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public WorkspaceRole Role { get; set; }

        public DateTime JoinedAt { get; set; }
    }
}
