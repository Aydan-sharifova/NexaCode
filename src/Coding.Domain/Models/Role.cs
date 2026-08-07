using System;
namespace Coding.Models
{
    public class Role : Base
    {

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public ICollection<UserRole> UserRoles { get; set; } = [];
    }
}
