using System;
namespace Coding.Models
{
    public class User : Base
    {

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string PublicId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string? Bio { get; set; }

        public bool IsOnline { get; set; }

        public DateTime LastSeen { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? EmailVerifiedAt { get; set; }
        public bool IsSuspended { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public string? SuspensionReason { get; set; }

        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

        public ICollection<AccountToken> AccountTokens { get; set; } = [];

        public ICollection<UserRole> UserRoles { get; set; } = [];

        public ICollection<WorkspaceMember> WorkspaceMembers { get; set; } = [];

        public ICollection<ProjectMember> ProjectMembers { get; set; } = [];

        public ICollection<Project> OwnedProjects { get; set; } = [];

        public ICollection<Message> Messages { get; set; } = [];

        public ICollection<Notification> Notifications { get; set; } = [];

        public ICollection<CodeHistory> CodeHistories { get; set; } = [];
    }
}
