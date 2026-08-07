using System;
using Coding.Models;

namespace Coding.Models
{
    public class FileItem:Base
    {

        public string Name { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public long Size { get; set; }

        public Guid FolderId { get; set; }

        public Folder Folder { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<CodeHistory> Histories { get; set; } = [];
    }
}
