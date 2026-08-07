using System;
using Coding.Enums;

namespace Coding.Models
{
    public class Invitation:Base
    {
        public Guid WorkspaceId { get; set; }

        public Workspace Workspace { get; set; } = null!;

        public string Email { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public InvitationStatus Status { get; set; }

        public DateTime ExpireDate { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
