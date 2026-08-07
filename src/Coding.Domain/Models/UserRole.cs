using System;
namespace Coding.Models
{
    public class UserRole : Base
    {
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public Guid RoleId { get; set; }

        public Role Role { get; set; } = null!;
    }
}
