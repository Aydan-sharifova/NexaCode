namespace Coding.Models;

public sealed class DeveloperProfile : Base
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Headline { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Location { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? GitHubUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? PrimaryRole { get; set; }
    public string? ExperienceLevel { get; set; }
    public string[] Skills { get; set; } = [];
    public string[] LearningTopics { get; set; } = [];
    public bool IsProfilePublic { get; set; } = true;
    public bool IsActivityPublic { get; set; } = true;
    public bool AreFollowersPublic { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
