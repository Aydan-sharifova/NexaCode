using Coding.Enums;

namespace Coding.Models;

public sealed class ProjectInvitation : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public ProjectRole Role { get; set; } = ProjectRole.Developer;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public Guid InvitedById { get; set; }
    public User InvitedBy { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
}
