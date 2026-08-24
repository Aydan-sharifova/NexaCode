using Coding.Enums;

namespace Coding.Domain.Services;

public static class LiveRoomLifecycle
{
    public static bool CanTransition(LiveRoomStatus current, LiveRoomStatus next) => (current, next) switch
    {
        (LiveRoomStatus.Scheduled, LiveRoomStatus.Active or LiveRoomStatus.Cancelled) => true,
        (LiveRoomStatus.Active, LiveRoomStatus.Completed or LiveRoomStatus.Cancelled) => true,
        _ => false
    };

    public static bool CanManage(LiveRoomParticipantRole role) => role is LiveRoomParticipantRole.Owner or LiveRoomParticipantRole.Host or LiveRoomParticipantRole.Interviewer;
}
