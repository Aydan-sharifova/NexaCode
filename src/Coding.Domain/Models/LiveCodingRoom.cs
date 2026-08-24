using Coding.Enums;

namespace Coding.Models;

public sealed class LiveCodingRoom : Base
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LiveRoomMode Mode { get; set; }
    public LiveRoomStatus Status { get; set; } = LiveRoomStatus.Scheduled;
    public LiveRoomVisibility Visibility { get; set; } = LiveRoomVisibility.InviteOnly;
    public LiveRoomChallengeType? ChallengeType { get; set; }
    public string? ProblemTitle { get; set; }
    public string? ProblemStatement { get; set; }
    public int? DurationMinutes { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long StateVersion { get; set; }
    public ICollection<RoomParticipant> Participants { get; set; } = [];
    public ICollection<RoomMessage> Messages { get; set; } = [];
    public ICollection<RoomTask> Tasks { get; set; } = [];
    public ICollection<RoomReaction> Reactions { get; set; } = [];
    public ICollection<RoomInterviewerNote> InterviewerNotes { get; set; } = [];
}

public sealed class RoomTask : Base
{
    public Guid RoomId { get; set; }
    public LiveCodingRoom Room { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LiveRoomTaskStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class RoomReaction : Base
{
    public Guid RoomId { get; set; }
    public LiveCodingRoom Room { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Emoji { get; set; } = string.Empty;
}

public sealed class RoomInterviewerNote : Base
{
    public Guid RoomId { get; set; }
    public LiveCodingRoom Room { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
}

public sealed class RoomParticipant : Base
{
    public Guid RoomId { get; set; }
    public LiveCodingRoom Room { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public LiveRoomParticipantRole Role { get; set; }
    public LiveRoomParticipantStatus Status { get; set; } = LiveRoomParticipantStatus.Invited;
    public Guid InvitedById { get; set; }
    public User InvitedBy { get; set; } = null!;
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
}

public sealed class RoomMessage : Base
{
    public Guid RoomId { get; set; }
    public LiveCodingRoom Room { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
