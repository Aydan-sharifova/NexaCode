using System;
namespace Coding.Models
{
    public class RefreshToken:Base
    {
        public string Token { get; set; } = string.Empty;

        public DateTime ExpireDate { get; set; }

        public bool IsRevoked { get; set; }

        public Guid UserId { get; set; }

        public User User { get; set; } = null!;
    }
}
