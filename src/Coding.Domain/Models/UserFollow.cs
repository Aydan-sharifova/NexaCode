namespace Coding.Models;

public sealed class UserFollow : Base
{
    public Guid FollowerId { get; set; }
    public User Follower { get; set; } = null!;
    public Guid FollowingId { get; set; }
    public User Following { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
