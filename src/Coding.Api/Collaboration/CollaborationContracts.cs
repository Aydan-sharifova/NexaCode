namespace Coding.Api.Collaboration;
using Coding.Application.Features.Chat;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.LiveRooms;

public sealed record TextRange(
    int StartLineNumber,
    int StartColumn,
    int EndLineNumber,
    int EndColumn);

public sealed record CodeOperation(
    Guid OperationId,
    Guid FileId,
    Guid UserId,
    long ClientVersion,
    long BaseVersion,
    TextRange Range,
    string InsertedText,
    int DeletedLength,
    DateTime Timestamp);

public sealed record CursorPosition(
    Guid FileId,
    int LineNumber,
    int Column,
    TextRange? Selection);

public sealed record CollaborationUser(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    int ConnectionCount,
    DateTime LastSeenAt);

public sealed record PresenceUpdate(Guid ProjectId, IReadOnlyCollection<CollaborationUser> Users);
public sealed record UserPresence(Guid ProjectId, CollaborationUser User);
public sealed record FileChangedMessage(Guid FileId, Guid ChangedByUserId, int VersionNumber, string ConcurrencyToken);
public sealed record ResyncRequiredMessage(Guid FileId, long ServerVersion, string Reason);
public sealed record OperationAcceptedMessage(Guid OperationId, Guid FileId, long ServerVersion);
public sealed record CollaborativeUpdateMessage(Guid ProjectId, Guid FileId, string ClientId, Guid UpdateId, string EncodedUpdate, string UpdateType, DateTime CreatedAt, string? PlainContent = null);
public sealed record CollaborativeStateMessage(string? Snapshot, IReadOnlyList<CollaborativeUpdateMessage> Updates, long SequenceNumber);

public interface ICollaborationClient
{
    Task UserJoined(UserPresence message);
    Task UserLeft(UserPresence message);
    Task PresenceUpdated(PresenceUpdate message);
    Task CodeOperationReceived(CodeOperation operation);
    Task OperationAccepted(OperationAcceptedMessage message);
    Task ResyncRequired(ResyncRequiredMessage message);
    Task CursorUpdated(Guid userId, CursorPosition position);
    Task TypingStarted(Guid fileId, Guid userId);
    Task TypingStopped(Guid fileId, Guid userId);
    Task FileChanged(FileChangedMessage message);
    Task DocumentUpdateReceived(CollaborativeUpdateMessage message);
    Task AwarenessUpdateReceived(CollaborativeUpdateMessage message);
    Task CollaborativeDocumentReset(CollaborativeUpdateMessage message);
    Task ReceiveMessage(ChatMessageItem message);
    Task MessageRead(Guid conversationId, Guid userId, Guid? throughMessageId, DateTime readAt);
    Task ConversationUpdated(Guid conversationId);
    Task ChatTypingStarted(Guid conversationId, Guid userId);
    Task ChatTypingStopped(Guid conversationId, Guid userId);
    Task ReceiveNotification(NotificationItem notification);
    Task NotificationRead(Guid? notificationId);
    Task UnreadCountUpdated(int count);
    Task LiveRoomStateChanged(LiveRoomStateEvent state);
    Task LiveRoomParticipantChanged(Guid roomId, LiveRoomParticipantItem participant);
    Task LiveRoomMessageCreated(LiveRoomMessageItem message);
    Task LiveRoomTaskChanged(LiveRoomTaskItem task);
    Task LiveRoomReactionCreated(LiveRoomReactionItem reaction);
}
