using Coding.Application.Features.LiveRooms;
using Coding.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, EnableRateLimiting("social"), Route("api/live-rooms")]
public sealed class LiveRoomsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<LiveRoomSummary>> List([FromQuery] LiveRoomStatus? status = null, CancellationToken ct = default) => sender.Send(new ListLiveRoomsQuery(status), ct);
    [HttpGet("{roomId:guid}")]
    public Task<LiveRoomDetails> Details(Guid roomId, CancellationToken ct) => sender.Send(new GetLiveRoomQuery(roomId), ct);
    [HttpPost]
    public async Task<ActionResult<LiveRoomDetails>> Create(CreateLiveRoomRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new CreateLiveRoomCommand(r.ProjectId, r.Title, r.Description, r.Mode, r.Visibility, r.ChallengeType, r.ProblemTitle, r.ProblemStatement, r.DurationMinutes, r.ScheduledAt), ct));
    [HttpPost("{roomId:guid}/participants")]
    public Task<LiveRoomDetails> Invite(Guid roomId, InviteLiveRoomRequest r, CancellationToken ct) => sender.Send(new InviteRoomParticipantCommand(roomId, r.UserPublicId, r.Role), ct);
    [HttpPost("{roomId:guid}/join")]
    public Task<LiveRoomDetails> Join(Guid roomId, CancellationToken ct) => sender.Send(new JoinLiveRoomCommand(roomId), ct);
    [HttpPost("{roomId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid roomId, CancellationToken ct) { await sender.Send(new LeaveLiveRoomCommand(roomId), ct); return NoContent(); }
    [HttpDelete("{roomId:guid}/participants/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid roomId, Guid userId, CancellationToken ct) { await sender.Send(new RemoveRoomParticipantCommand(roomId, userId), ct); return NoContent(); }
    [HttpPut("{roomId:guid}/status")]
    public Task<LiveRoomDetails> Status(Guid roomId, SetLiveRoomStatusRequest r, CancellationToken ct) => sender.Send(new SetLiveRoomStatusCommand(roomId, r.Status, r.ExpectedStateVersion), ct);
    [HttpGet("{roomId:guid}/messages")]
    public Task<IReadOnlyList<LiveRoomMessageItem>> Messages(Guid roomId, [FromQuery] DateTime? before = null, [FromQuery] int take = 50, CancellationToken ct = default) => sender.Send(new ListRoomMessagesQuery(roomId, before, take), ct);
    [HttpPost("{roomId:guid}/messages")]
    public async Task<ActionResult<LiveRoomMessageItem>> Message(Guid roomId, SendLiveRoomMessageRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new SendRoomMessageCommand(roomId, r.Content), ct));
    [HttpGet("{roomId:guid}/tasks")]
    public Task<IReadOnlyList<LiveRoomTaskItem>> Tasks(Guid roomId, CancellationToken ct) => sender.Send(new ListRoomTasksQuery(roomId), ct);
    [HttpPost("{roomId:guid}/tasks")]
    public async Task<ActionResult<LiveRoomTaskItem>> Task(Guid roomId, CreateRoomTaskRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new CreateRoomTaskCommand(roomId, r.Title, r.Description), ct));
    [HttpPut("{roomId:guid}/tasks/{taskId:guid}/status")]
    public Task<LiveRoomTaskItem> TaskStatus(Guid roomId, Guid taskId, SetRoomTaskStatusRequest r, CancellationToken ct) => sender.Send(new SetRoomTaskStatusCommand(roomId, taskId, r.Status), ct);
    [HttpPost("{roomId:guid}/reactions")]
    public async Task<ActionResult<LiveRoomReactionItem>> Reaction(Guid roomId, AddRoomReactionRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new AddRoomReactionCommand(roomId, r.Emoji), ct));
    [HttpGet("{roomId:guid}/interviewer-notes")]
    public Task<IReadOnlyList<LiveRoomInterviewerNoteItem>> Notes(Guid roomId, CancellationToken ct) => sender.Send(new ListInterviewerNotesQuery(roomId), ct);
    [HttpPost("{roomId:guid}/interviewer-notes")]
    public async Task<ActionResult<LiveRoomInterviewerNoteItem>> Note(Guid roomId, SaveInterviewerNoteRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new SaveInterviewerNoteCommand(roomId, r.NoteId, r.Content), ct));
}

public sealed record CreateLiveRoomRequest(Guid? ProjectId, string Title, string? Description, LiveRoomMode Mode, LiveRoomVisibility Visibility, LiveRoomChallengeType? ChallengeType, string? ProblemTitle, string? ProblemStatement, int? DurationMinutes, DateTime? ScheduledAt);
public sealed record InviteLiveRoomRequest(string UserPublicId, LiveRoomParticipantRole Role);
public sealed record SetLiveRoomStatusRequest(LiveRoomStatus Status, long ExpectedStateVersion);
public sealed record SendLiveRoomMessageRequest(string Content);
public sealed record CreateRoomTaskRequest(string Title, string? Description);
public sealed record SetRoomTaskStatusRequest(LiveRoomTaskStatus Status);
public sealed record AddRoomReactionRequest(string Emoji);
public sealed record SaveInterviewerNoteRequest(Guid? NoteId, string Content);
