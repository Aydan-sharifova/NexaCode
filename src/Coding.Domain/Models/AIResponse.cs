using System;
using Coding.Models;

namespace Coding.Models
{
    public class AIResponse:Base
    {
        public Guid AIRequestId { get; set; }

        public AIRequest AIRequest { get; set; } = null!;

        public string ResponseText { get; set; } = string.Empty;

        public int PromptTokens { get; set; }

        public int CompletionTokens { get; set; }

        public DateTime GeneratedAt { get; set; }
    }
}
