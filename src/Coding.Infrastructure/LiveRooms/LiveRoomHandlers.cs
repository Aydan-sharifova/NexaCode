using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.LiveRooms;
using Coding.Application.Features.Notifications;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Coding.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.LiveRooms;

internal static class LiveRoomAccess
{
    public static LiveRoomUser User(User x) => new(x.ID, x.PublicId, x.UserName, $"{x.FirstName} {x.LastName}".Trim(), x.AvatarUrl);
    public static LiveRoomParticipantItem Participant(RoomParticipant x) => new(x.ID, User(x.User), x.Role, x.Status, x.InvitedAt, x.JoinedAt, x.LeftAt);
    public static IQueryable<LiveCodingRoom> Graph(AppDbContext db) => db.LiveCodingRooms.Include(x => x.Owner).Include(x => x.Participants).ThenInclude(x => x.User).AsSplitQuery();

    public static async Task<LiveCodingRoom> RequireAsync(AppDbContext db, Guid roomId, Guid userId, CancellationToken ct)
    {
        var room = await Graph(db).SingleOrDefaultAsync(x => x.ID == roomId, ct) ?? throw new NotFoundException("Live room not found.");
        if (!CanAccess(room, userId) && !(room.Visibility == LiveRoomVisibility.ProjectMembers && room.ProjectId.HasValue && await db.ProjectMembers.AnyAsync(x => x.ProjectId == room.ProjectId && x.UserId == userId, ct)))
            throw new ForbiddenException("You are not invited to this live room.");
        return room;
    }

    public static bool CanAccess(LiveCodingRoom room, Guid userId) => room.OwnerId == userId || room.Participants.Any(x => x.UserId == userId && x.Status != LiveRoomParticipantStatus.Removed);
    public static RoomParticipant? Current(LiveCodingRoom room, Guid userId) => room.Participants.SingleOrDefault(x => x.UserId == userId);
    public static bool CanManage(LiveCodingRoom room, Guid userId) => room.OwnerId == userId || (Current(room, userId) is { } participant && LiveRoomLifecycle.CanManage(participant.Role));
    public static void RequireManage(LiveCodingRoom room, Guid userId) { if (!CanManage(room, userId)) throw new ForbiddenException("Room host access is required."); }

    public static LiveRoomSummary Summary(LiveCodingRoom x, Guid userId)
    {
        var current = Current(x, userId);
        var role = x.OwnerId == userId ? LiveRoomParticipantRole.Owner : current?.Role ?? LiveRoomParticipantRole.Participant;
        return new(x.ID, x.ProjectId, x.Title, x.Description, x.Mode, x.Status, x.Visibility, x.ChallengeType, x.ProblemTitle, x.DurationMinutes, x.ScheduledAt, x.StartedAt, x.CompletedAt, x.StateVersion, User(x.Owner), x.Participants.Count(p => p.Status is LiveRoomParticipantStatus.Invited or LiveRoomParticipantStatus.Joined or LiveRoomParticipantStatus.Left), role);
    }

    public static LiveRoomDetails Details(LiveCodingRoom x, Guid userId)
    {
        var canManage = CanManage(x, userId);
        return new(Summary(x, userId), x.ProblemStatement, x.Participants.Where(p => p.Status != LiveRoomParticipantStatus.Removed).OrderBy(p => p.Role).ThenBy(p => p.InvitedAt).Select(Participant).ToArray(), canManage, canManage && x.Status == LiveRoomStatus.Scheduled, canManage && x.Status == LiveRoomStatus.Active);
    }

    public static LiveRoomStateEvent State(LiveCodingRoom x) => new(x.ID, x.Status, x.StartedAt, x.CompletedAt, x.DurationMinutes, x.StateVersion, DateTime.UtcNow);
}

public sealed class CreateLiveRoomHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity) : IRequestHandler<CreateLiveRoomCommand, LiveRoomDetails>
{
    public async Task<LiveRoomDetails> Handle(CreateLiveRoomCommand r, CancellationToken ct)
    {
        if (r.ProjectId.HasValue) await ProjectAccess.RequireWorkspaceWriteAsync(db, r.ProjectId.Value, user.UserId, ct);
        var owner = await db.Users.SingleAsync(x => x.ID == user.UserId, ct);
        var room = new LiveCodingRoom { ID = Guid.NewGuid(), OwnerId = user.UserId, Owner = owner, ProjectId = r.ProjectId, Title = r.Title.Trim(), Description = r.Description?.Trim(), Mode = r.Mode, Visibility = r.Visibility, ChallengeType = r.ChallengeType, ProblemTitle = r.ProblemTitle?.Trim(), ProblemStatement = r.ProblemStatement?.Trim(), DurationMinutes = r.DurationMinutes, ScheduledAt = r.ScheduledAt?.ToUniversalTime() };
        room.Participants.Add(new RoomParticipant { ID = Guid.NewGuid(), UserId = user.UserId, User = owner, InvitedById = user.UserId, Role = LiveRoomParticipantRole.Owner, Status = LiveRoomParticipantStatus.Joined, JoinedAt = DateTime.UtcNow });
        db.LiveCodingRooms.Add(room); await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, room.ProjectId, "LiveRoomCreated", nameof(LiveCodingRoom), room.ID, $"Created {room.Mode} room '{room.Title}'."), ct);
        return LiveRoomAccess.Details(room, user.UserId);
    }
}

public sealed class ListLiveRoomsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListLiveRoomsQuery, IReadOnlyList<LiveRoomSummary>>
{
    public async Task<IReadOnlyList<LiveRoomSummary>> Handle(ListLiveRoomsQuery r, CancellationToken ct)
    {
        var query = LiveRoomAccess.Graph(db).Where(x => x.OwnerId == user.UserId || x.Participants.Any(p => p.UserId == user.UserId && p.Status != LiveRoomParticipantStatus.Removed) || (x.Visibility == LiveRoomVisibility.ProjectMembers && x.ProjectId != null && db.ProjectMembers.Any(p => p.ProjectId == x.ProjectId && p.UserId == user.UserId)));
        if (r.Status.HasValue) query = query.Where(x => x.Status == r.Status);
        return (await query.OrderBy(x => x.Status).ThenByDescending(x => x.ScheduledAt ?? x.CreatAt).Take(100).ToListAsync(ct)).Select(x => LiveRoomAccess.Summary(x, user.UserId)).ToArray();
    }
}

public sealed class GetLiveRoomHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetLiveRoomQuery, LiveRoomDetails>
{
    public async Task<LiveRoomDetails> Handle(GetLiveRoomQuery r, CancellationToken ct) => LiveRoomAccess.Details(await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct), user.UserId);
}

public sealed class InviteRoomParticipantHandler(AppDbContext db, ICurrentUser user, INotificationService notifications, ILiveRoomRealtimePublisher realtime) : IRequestHandler<InviteRoomParticipantCommand, LiveRoomDetails>
{
    public async Task<LiveRoomDetails> Handle(InviteRoomParticipantCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); LiveRoomAccess.RequireManage(room, user.UserId);
        if (room.Status is LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) throw new ConflictException("A closed room cannot accept invitations.");
        if (r.Role == LiveRoomParticipantRole.Owner) throw new ConflictException("Room ownership cannot be assigned through an invitation.");
        var publicId = r.UserPublicId.Trim().TrimStart('@');
        var target = await db.Users.SingleOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted && !x.IsSuspended, ct) ?? throw new NotFoundException("User not found.");
        var participant = room.Participants.SingleOrDefault(x => x.UserId == target.ID);
        if (participant is null) { participant = new() { ID = Guid.NewGuid(), RoomId = room.ID, UserId = target.ID, User = target, InvitedById = user.UserId, Role = r.Role }; db.RoomParticipants.Add(participant); }
        else { participant.Role = r.Role; participant.Status = LiveRoomParticipantStatus.Invited; participant.LeftAt = null; participant.IsDeleted = false; participant.DeletedAt = null; }
        await db.SaveChangesAsync(ct);
        await notifications.CreateAsync(new(target.ID, NotificationType.Invitation, "Live room invitation", $"You were invited to '{room.Title}'.", room.ID, nameof(LiveCodingRoom)), ct);
        await realtime.ParticipantChangedAsync(room.ID, LiveRoomAccess.Participant(participant), ct);
        return LiveRoomAccess.Details(room, user.UserId);
    }
}

public sealed class JoinLiveRoomHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<JoinLiveRoomCommand, LiveRoomDetails>
{
    public async Task<LiveRoomDetails> Handle(JoinLiveRoomCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct);
        if (room.Status is LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) throw new ConflictException("This live room is closed.");
        var participant = LiveRoomAccess.Current(room, user.UserId);
        if (participant is null)
        {
            var target = await db.Users.SingleAsync(x => x.ID == user.UserId, ct);
            participant = new() { ID = Guid.NewGuid(), RoomId = room.ID, UserId = user.UserId, User = target, InvitedById = room.OwnerId, Role = LiveRoomParticipantRole.Participant };
            db.RoomParticipants.Add(participant);
        }
        participant.Status = LiveRoomParticipantStatus.Joined; participant.JoinedAt ??= DateTime.UtcNow; participant.LeftAt = null; await db.SaveChangesAsync(ct);
        await realtime.ParticipantChangedAsync(room.ID, LiveRoomAccess.Participant(participant), ct);
        return LiveRoomAccess.Details(room, user.UserId);
    }
}

public sealed class LeaveLiveRoomHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<LeaveLiveRoomCommand>
{
    public async Task Handle(LeaveLiveRoomCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); var participant = LiveRoomAccess.Current(room, user.UserId) ?? throw new NotFoundException("Room participant not found.");
        if (participant.Role == LiveRoomParticipantRole.Owner && room.Status == LiveRoomStatus.Active) throw new ConflictException("Complete or cancel the active room before leaving as owner.");
        participant.Status = LiveRoomParticipantStatus.Left; participant.LeftAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await realtime.ParticipantChangedAsync(room.ID, LiveRoomAccess.Participant(participant), ct);
    }
}

public sealed class RemoveRoomParticipantHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<RemoveRoomParticipantCommand>
{
    public async Task Handle(RemoveRoomParticipantCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); LiveRoomAccess.RequireManage(room, user.UserId);
        if (r.UserId == room.OwnerId) throw new ConflictException("The room owner cannot be removed.");
        var participant = room.Participants.SingleOrDefault(x => x.UserId == r.UserId) ?? throw new NotFoundException("Room participant not found.");
        participant.Status = LiveRoomParticipantStatus.Removed; participant.LeftAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await realtime.ParticipantChangedAsync(room.ID, LiveRoomAccess.Participant(participant), ct);
    }
}

public sealed class SetLiveRoomStatusHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime, IActivityLogger activity) : IRequestHandler<SetLiveRoomStatusCommand, LiveRoomDetails>
{
    public async Task<LiveRoomDetails> Handle(SetLiveRoomStatusCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); LiveRoomAccess.RequireManage(room, user.UserId);
        if (!LiveRoomLifecycle.CanTransition(room.Status, r.Status)) throw new ConflictException($"Live room cannot transition from {room.Status} to {r.Status}.");
        if (room.StateVersion != r.ExpectedStateVersion) throw new ConflictException("The live room state changed. Refresh before retrying.");
        var now = DateTime.UtcNow; room.Status = r.Status; room.StateVersion++;
        if (r.Status == LiveRoomStatus.Active) room.StartedAt = now;
        if (r.Status is LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) room.CompletedAt = now;
        await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, room.ProjectId, $"LiveRoom{r.Status}", nameof(LiveCodingRoom), room.ID, $"Live room '{room.Title}' changed to {r.Status}."), ct);
        await realtime.StateChangedAsync(LiveRoomAccess.State(room), ct);
        return LiveRoomAccess.Details(room, user.UserId);
    }
}

public sealed class ListRoomMessagesHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListRoomMessagesQuery, IReadOnlyList<LiveRoomMessageItem>>
{
    public async Task<IReadOnlyList<LiveRoomMessageItem>> Handle(ListRoomMessagesQuery r, CancellationToken ct)
    {
        await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct);
        var query = db.RoomMessages.Include(x => x.Author).Where(x => x.RoomId == r.RoomId);
        if (r.Before.HasValue) query = query.Where(x => x.SentAt < r.Before.Value);
        var messages = await query.OrderByDescending(x => x.SentAt).Take(Math.Clamp(r.Take, 1, 100)).ToListAsync(ct);
        return messages.OrderBy(x => x.SentAt).Select(Message).ToArray();
    }
    internal static LiveRoomMessageItem Message(RoomMessage x) => new(x.ID, x.RoomId, LiveRoomAccess.User(x.Author), x.Content, x.SentAt);
}

public sealed class SendRoomMessageHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<SendRoomMessageCommand, LiveRoomMessageItem>
{
    public async Task<LiveRoomMessageItem> Handle(SendRoomMessageCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct);
        if (room.Status is LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) throw new ConflictException("Messages cannot be sent to a closed room.");
        var participant = LiveRoomAccess.Current(room, user.UserId);
        if (participant?.Status != LiveRoomParticipantStatus.Joined) throw new ForbiddenException("Join the room before sending messages.");
        var author = await db.Users.SingleAsync(x => x.ID == user.UserId, ct);
        var message = new RoomMessage { ID = Guid.NewGuid(), RoomId = room.ID, AuthorId = user.UserId, Author = author, Content = r.Content.Trim(), SentAt = DateTime.UtcNow };
        db.RoomMessages.Add(message); await db.SaveChangesAsync(ct);
        var dto = ListRoomMessagesHandler.Message(message); await realtime.MessageCreatedAsync(dto, ct); return dto;
    }
}

public sealed class ListRoomTasksHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListRoomTasksQuery, IReadOnlyList<LiveRoomTaskItem>>
{
    public async Task<IReadOnlyList<LiveRoomTaskItem>> Handle(ListRoomTasksQuery r, CancellationToken ct)
    {
        await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct);
        return await db.RoomTasks.Include(x => x.CreatedBy).Where(x => x.RoomId == r.RoomId).OrderBy(x => x.CreatAt)
            .Select(x => new LiveRoomTaskItem(x.ID, x.RoomId, LiveRoomAccess.User(x.CreatedBy), x.Title, x.Description, x.Status, x.CreatAt, x.CompletedAt)).ToArrayAsync(ct);
    }
}

public sealed class CreateRoomTaskHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<CreateRoomTaskCommand, LiveRoomTaskItem>
{
    public async Task<LiveRoomTaskItem> Handle(CreateRoomTaskCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); LiveRoomAccess.RequireManage(room, user.UserId);
        if (room.Status is LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) throw new ConflictException("Tasks cannot be added to a closed room.");
        var author = await db.Users.SingleAsync(x => x.ID == user.UserId, ct);
        var task = new RoomTask { ID = Guid.NewGuid(), RoomId = room.ID, CreatedById = user.UserId, CreatedBy = author, Title = r.Title.Trim(), Description = r.Description?.Trim() };
        db.RoomTasks.Add(task); await db.SaveChangesAsync(ct);
        var dto = new LiveRoomTaskItem(task.ID, task.RoomId, LiveRoomAccess.User(author), task.Title, task.Description, task.Status, task.CreatAt, task.CompletedAt);
        await realtime.TaskChangedAsync(dto, ct); return dto;
    }
}

public sealed class SetRoomTaskStatusHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<SetRoomTaskStatusCommand, LiveRoomTaskItem>
{
    public async Task<LiveRoomTaskItem> Handle(SetRoomTaskStatusCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct);
        if (room.Status is LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) throw new ConflictException("Tasks cannot change in a closed room.");
        if (LiveRoomAccess.Current(room, user.UserId)?.Status != LiveRoomParticipantStatus.Joined) throw new ForbiddenException("Join the room before updating tasks.");
        var task = await db.RoomTasks.Include(x => x.CreatedBy).SingleOrDefaultAsync(x => x.ID == r.TaskId && x.RoomId == r.RoomId, ct) ?? throw new NotFoundException("Room task not found.");
        task.Status = r.Status; task.CompletedAt = r.Status == LiveRoomTaskStatus.Completed ? DateTime.UtcNow : null; task.UpdateAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        var dto = new LiveRoomTaskItem(task.ID, task.RoomId, LiveRoomAccess.User(task.CreatedBy), task.Title, task.Description, task.Status, task.CreatAt, task.CompletedAt);
        await realtime.TaskChangedAsync(dto, ct); return dto;
    }
}

public sealed class AddRoomReactionHandler(AppDbContext db, ICurrentUser user, ILiveRoomRealtimePublisher realtime) : IRequestHandler<AddRoomReactionCommand, LiveRoomReactionItem>
{
    public async Task<LiveRoomReactionItem> Handle(AddRoomReactionCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct);
        if (room.Status != LiveRoomStatus.Active || LiveRoomAccess.Current(room, user.UserId)?.Status != LiveRoomParticipantStatus.Joined) throw new ConflictException("Reactions are available to joined participants while the room is active.");
        var author = await db.Users.SingleAsync(x => x.ID == user.UserId, ct);
        var reaction = await db.RoomReactions.SingleOrDefaultAsync(x => x.RoomId == room.ID && x.UserId == user.UserId && x.Emoji == r.Emoji, ct);
        if (reaction is null) { reaction = new RoomReaction { ID = Guid.NewGuid(), RoomId = room.ID, UserId = user.UserId, User = author, Emoji = r.Emoji }; db.RoomReactions.Add(reaction); }
        else { reaction.CreatAt = DateTime.UtcNow; reaction.UpdateAt = DateTime.UtcNow; reaction.User = author; }
        await db.SaveChangesAsync(ct);
        var dto = new LiveRoomReactionItem(reaction.ID, reaction.RoomId, LiveRoomAccess.User(author), reaction.Emoji, reaction.CreatAt);
        await realtime.ReactionCreatedAsync(dto, ct); return dto;
    }
}

internal static class InterviewerNoteAccess
{
    public static void Require(LiveCodingRoom room, Guid userId)
    {
        var role = room.OwnerId == userId ? LiveRoomParticipantRole.Owner : LiveRoomAccess.Current(room, userId)?.Role;
        if (room.Mode != LiveRoomMode.Interview || role is not (LiveRoomParticipantRole.Owner or LiveRoomParticipantRole.Host or LiveRoomParticipantRole.Interviewer))
            throw new ForbiddenException("Private interviewer notes are restricted to interview staff.");
    }
}

public sealed class ListInterviewerNotesHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListInterviewerNotesQuery, IReadOnlyList<LiveRoomInterviewerNoteItem>>
{
    public async Task<IReadOnlyList<LiveRoomInterviewerNoteItem>> Handle(ListInterviewerNotesQuery r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); InterviewerNoteAccess.Require(room, user.UserId);
        return await db.RoomInterviewerNotes.Include(x => x.Author).Where(x => x.RoomId == r.RoomId).OrderBy(x => x.CreatAt)
            .Select(x => new LiveRoomInterviewerNoteItem(x.ID, x.RoomId, LiveRoomAccess.User(x.Author), x.Content, x.CreatAt, x.UpdateAt ?? x.CreatAt)).ToArrayAsync(ct);
    }
}

public sealed class SaveInterviewerNoteHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<SaveInterviewerNoteCommand, LiveRoomInterviewerNoteItem>
{
    public async Task<LiveRoomInterviewerNoteItem> Handle(SaveInterviewerNoteCommand r, CancellationToken ct)
    {
        var room = await LiveRoomAccess.RequireAsync(db, r.RoomId, user.UserId, ct); InterviewerNoteAccess.Require(room, user.UserId);
        RoomInterviewerNote note;
        if (r.NoteId.HasValue)
        {
            note = await db.RoomInterviewerNotes.Include(x => x.Author).SingleOrDefaultAsync(x => x.ID == r.NoteId && x.RoomId == r.RoomId, ct) ?? throw new NotFoundException("Interviewer note not found.");
            if (note.AuthorId != user.UserId) throw new ForbiddenException("Only the note author can edit this private note.");
            note.Content = r.Content.Trim(); note.UpdateAt = DateTime.UtcNow;
        }
        else
        {
            var author = await db.Users.SingleAsync(x => x.ID == user.UserId, ct);
            note = new RoomInterviewerNote { ID = Guid.NewGuid(), RoomId = room.ID, AuthorId = user.UserId, Author = author, Content = r.Content.Trim() }; db.RoomInterviewerNotes.Add(note);
        }
        await db.SaveChangesAsync(ct);
        return new(note.ID, note.RoomId, LiveRoomAccess.User(note.Author), note.Content, note.CreatAt, note.UpdateAt ?? note.CreatAt);
    }
}
