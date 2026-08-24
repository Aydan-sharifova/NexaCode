namespace Coding.Models;

public sealed class ProjectView
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; }=null!;
    public Guid UserId { get; set; }
    public User User { get; set; }=null!;
    public DateOnly ViewedOn { get; set; }
    public DateTime ViewedAt { get; set; }
}
