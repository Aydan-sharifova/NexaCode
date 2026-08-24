namespace Coding.Models;

public sealed class Achievement : Base
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Points { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserAchievement> Awards { get; set; } = [];
}

public sealed class UserAchievement : Base
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid AchievementId { get; set; }
    public Achievement Achievement { get; set; } = null!;
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    public string EvidenceType { get; set; } = string.Empty;
    public Guid? EvidenceId { get; set; }
    public string EvidenceJson { get; set; } = "{}";
    public bool IsVerified { get; set; } = true;
}
