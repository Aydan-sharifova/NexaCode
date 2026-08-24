using Coding.Application.Features.LiveRooms;
using Microsoft.AspNetCore.SignalR;

namespace Coding.Api.Collaboration;

public sealed class LiveRoomRealtimePublisher(IHubContext<CollaborationHub, ICollaborationClient> hub) : ILiveRoomRealtimePublisher
{
    public Task StateChangedAsync(LiveRoomStateEvent state, CancellationToken ct) => hub.Clients.Group(CollaborationHub.LiveRoomGroup(state.RoomId)).LiveRoomStateChanged(state);
    public Task ParticipantChangedAsync(Guid roomId, LiveRoomParticipantItem participant, CancellationToken ct) => hub.Clients.Group(CollaborationHub.LiveRoomGroup(roomId)).LiveRoomParticipantChanged(roomId, participant);
    public Task MessageCreatedAsync(LiveRoomMessageItem message, CancellationToken ct) => hub.Clients.Group(CollaborationHub.LiveRoomGroup(message.RoomId)).LiveRoomMessageCreated(message);
    public Task TaskChangedAsync(LiveRoomTaskItem task, CancellationToken ct) => hub.Clients.Group(CollaborationHub.LiveRoomGroup(task.RoomId)).LiveRoomTaskChanged(task);
    public Task ReactionCreatedAsync(LiveRoomReactionItem reaction, CancellationToken ct) => hub.Clients.Group(CollaborationHub.LiveRoomGroup(reaction.RoomId)).LiveRoomReactionCreated(reaction);
}
