using System;
using Coding.Models;

namespace Coding.Models
{
    public class CodeHistory:Base
    {

        public Guid FileItemId { get; set; }

        public FileItem FileItem { get; set; } = null!;

        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public string OldContent { get; set; } = string.Empty;

        public string NewContent { get; set; } = string.Empty;

        public DateTime EditedAt { get; set; }
    }
}
