using System;
namespace Coding.Models
{
    public class Message:Base
    {
        public Guid WorkspaceId { get; set; }

        public Workspace Workspace { get; set; } = null!;

        public Guid SenderId { get; set; }

        public User Sender { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }

        public bool IsEdited { get; set; }

    }
}
