using Coding.Enums;

namespace Coding.Models;

public sealed class UserBan
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BannedByUserId { get; set; }
    public User BannedByUser { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsPermanent { get; set; }
    public UserBanStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
