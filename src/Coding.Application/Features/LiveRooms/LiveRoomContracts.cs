using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.LiveRooms;

public sealed record LiveRoomUser(Guid Id, string PublicId, string UserName, string FullName, string? AvatarUrl);
public sealed record LiveRoomParticipantItem(Guid Id, LiveRoomUser User, LiveRoomParticipantRole Role, LiveRoomParticipantStatus Status, DateTime InvitedAt, DateTime? JoinedAt, DateTime? LeftAt);
public sealed record LiveRoomSummary(Guid Id, Guid? ProjectId, string Title, string? Description, LiveRoomMode Mode, LiveRoomStatus Status, LiveRoomVisibility Visibility, LiveRoomChallengeType? ChallengeType, string? ProblemTitle, int? DurationMinutes, DateTime? ScheduledAt, DateTime? StartedAt, DateTime? CompletedAt, long StateVersion, LiveRoomUser Owner, int ParticipantCount, LiveRoomParticipantRole CurrentUserRole);
public sealed record LiveRoomDetails(LiveRoomSummary Room, string? ProblemStatement, IReadOnlyList<LiveRoomParticipantItem> Participants, bool CanManage, bool CanStart, bool CanComplete);
public sealed record LiveRoomMessageItem(Guid Id, Guid RoomId, LiveRoomUser Author, string Content, DateTime SentAt);
public sealed record LiveRoomStateEvent(Guid RoomId, LiveRoomStatus Status, DateTime? StartedAt, DateTime? CompletedAt, int? DurationMinutes, long StateVersion, DateTime ServerTime);
public sealed record LiveRoomTaskItem(Guid Id, Guid RoomId, LiveRoomUser CreatedBy, string Title, string? Description, LiveRoomTaskStatus Status, DateTime CreatedAt, DateTime? CompletedAt);
public sealed record LiveRoomReactionItem(Guid Id, Guid RoomId, LiveRoomUser User, string Emoji, DateTime CreatedAt);
public sealed record LiveRoomInterviewerNoteItem(Guid Id, Guid RoomId, LiveRoomUser Author, string Content, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CreateLiveRoomCommand(Guid? ProjectId, string Title, string? Description, LiveRoomMode Mode, LiveRoomVisibility Visibility, LiveRoomChallengeType? ChallengeType, string? ProblemTitle, string? ProblemStatement, int? DurationMinutes, DateTime? ScheduledAt) : IRequest<LiveRoomDetails>;
public sealed record ListLiveRoomsQuery(LiveRoomStatus? Status = null) : IRequest<IReadOnlyList<LiveRoomSummary>>;
public sealed record GetLiveRoomQuery(Guid RoomId) : IRequest<LiveRoomDetails>;
public sealed record InviteRoomParticipantCommand(Guid RoomId, string UserPublicId, LiveRoomParticipantRole Role) : IRequest<LiveRoomDetails>;
public sealed record JoinLiveRoomCommand(Guid RoomId) : IRequest<LiveRoomDetails>;
public sealed record LeaveLiveRoomCommand(Guid RoomId) : IRequest;
public sealed record RemoveRoomParticipantCommand(Guid RoomId, Guid UserId) : IRequest;
public sealed record SetLiveRoomStatusCommand(Guid RoomId, LiveRoomStatus Status, long ExpectedStateVersion) : IRequest<LiveRoomDetails>;
public sealed record ListRoomMessagesQuery(Guid RoomId, DateTime? Before = null, int Take = 50) : IRequest<IReadOnlyList<LiveRoomMessageItem>>;
public sealed record SendRoomMessageCommand(Guid RoomId, string Content) : IRequest<LiveRoomMessageItem>;
public sealed record ListRoomTasksQuery(Guid RoomId) : IRequest<IReadOnlyList<LiveRoomTaskItem>>;
public sealed record CreateRoomTaskCommand(Guid RoomId, string Title, string? Description) : IRequest<LiveRoomTaskItem>;
public sealed record SetRoomTaskStatusCommand(Guid RoomId, Guid TaskId, LiveRoomTaskStatus Status) : IRequest<LiveRoomTaskItem>;
public sealed record AddRoomReactionCommand(Guid RoomId, string Emoji) : IRequest<LiveRoomReactionItem>;
public sealed record ListInterviewerNotesQuery(Guid RoomId) : IRequest<IReadOnlyList<LiveRoomInterviewerNoteItem>>;
public sealed record SaveInterviewerNoteCommand(Guid RoomId, Guid? NoteId, string Content) : IRequest<LiveRoomInterviewerNoteItem>;

public interface ILiveRoomRealtimePublisher
{
    Task StateChangedAsync(LiveRoomStateEvent state, CancellationToken ct);
    Task ParticipantChangedAsync(Guid roomId, LiveRoomParticipantItem participant, CancellationToken ct);
    Task MessageCreatedAsync(LiveRoomMessageItem message, CancellationToken ct);
    Task TaskChangedAsync(LiveRoomTaskItem task, CancellationToken ct);
    Task ReactionCreatedAsync(LiveRoomReactionItem reaction, CancellationToken ct);
}

public sealed class CreateRoomTaskValidator : AbstractValidator<CreateRoomTaskCommand>
{
    public CreateRoomTaskValidator() { RuleFor(x => x.Title).NotEmpty().MaximumLength(240); RuleFor(x => x.Description).MaximumLength(4000); }
}

public sealed class AddRoomReactionValidator : AbstractValidator<AddRoomReactionCommand>
{
    private static readonly string[] Allowed = ["👍", "👏", "🎉", "💡", "❤️", "🚀"];
    public AddRoomReactionValidator() => RuleFor(x => x.Emoji).Must(Allowed.Contains).WithMessage("Unsupported room reaction.");
}

public sealed class SaveInterviewerNoteValidator : AbstractValidator<SaveInterviewerNoteCommand>
{
    public SaveInterviewerNoteValidator() => RuleFor(x => x.Content).NotEmpty().MaximumLength(8000);
}

public sealed class CreateLiveRoomValidator : AbstractValidator<CreateLiveRoomCommand>
{
    public CreateLiveRoomValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.ProblemTitle).MaximumLength(240);
        RuleFor(x => x.ProblemStatement).MaximumLength(20_000);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480).When(x => x.DurationMinutes.HasValue);
        RuleFor(x => x.ScheduledAt).Must(value => value is null || value > DateTime.UtcNow.AddMinutes(-1)).WithMessage("Scheduled time must be in the future.");
        RuleFor(x => x).Must(x => x.Mode != LiveRoomMode.Interview || x.ChallengeType is not null).WithMessage("Interview rooms require a challenge type.");
    }
}

public sealed class SendRoomMessageValidator : AbstractValidator<SendRoomMessageCommand>
{
    public SendRoomMessageValidator() => RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
}
