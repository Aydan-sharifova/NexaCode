using System;
using Coding.Enums;

namespace Coding.Models
{
    public class Notification:Base
    {
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid? RelatedEntityId { get; set; }

        public string? RelatedEntityType { get; set; }

        public DateTime? ReadAt { get; set; }
    }
}
