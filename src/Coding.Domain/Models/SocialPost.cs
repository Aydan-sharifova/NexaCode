using Coding.Enums;

namespace Coding.Models;

public sealed class SocialPost : Base
{
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public PostType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CodeLanguage { get; set; }
    public string? ImageUrl { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<SocialPostComment> Comments { get; set; } = [];
    public ICollection<SocialPostReaction> Reactions { get; set; } = [];
    public ICollection<SavedSocialPost> Saves { get; set; } = [];
    public ICollection<SocialPostShare> Shares { get; set; } = [];
}

public sealed class SocialPostComment : Base
{
    public Guid PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public Guid? ParentCommentId { get; set; }
    public SocialPostComment? ParentComment { get; set; }
    public ICollection<SocialPostComment> Replies { get; set; } = [];
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SocialPostReaction : Base
{
    public Guid PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class SavedSocialPost : Base
{
    public Guid PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class SocialPostShare : Base
{
    public Guid PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
