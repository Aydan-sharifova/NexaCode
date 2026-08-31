using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Coding.Data;
using Coding.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Coding.Application.Features.Collaboration;

namespace Coding.Api.Collaboration;

[Authorize, EnableRateLimiting("realtime")]
public sealed class CollaborationHub(
    AppDbContext db,
    ICollaborationPresenceTracker presence,
    ICollaborativeDocumentStore documentStore,
    ICollaborativeContentMaterializer materializer,
    ILogger<CollaborationHub> logger) : Hub<ICollaborationClient>
{
    private static readonly ConcurrentDictionary<Guid, long> LiveVersions = new();
    private static readonly ConcurrentDictionary<string, long> LastCursorTicks = new();
    private static readonly TimeSpan CursorInterval = TimeSpan.FromMilliseconds(50);

    private Guid UserId => Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
        ? id
        : throw new HubException("The authenticated user identifier is invalid.");

    public override async Task OnConnectedAsync()
    {
        var userId = UserId;
        var user = await db.Users.AsNoTracking()
            .Where(item => item.ID == userId && !item.IsDeleted)
            .Select(item => new { item.UserName, item.FirstName, item.LastName, item.AvatarUrl })
            .SingleOrDefaultAsync(Context.ConnectionAborted)
            ?? throw new HubException("Authenticated user no longer exists.");

        presence.Connect(
            Context.ConnectionId,
            userId,
            user.UserName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.AvatarUrl);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        logger.LogInformation(
            "Collaboration connection {ConnectionId} established for user {UserId}",
            Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connection = presence.Disconnect(Context.ConnectionId);
        if (connection is not null)
        {
            foreach (var projectId in connection.ProjectIds)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId));
                if (presence.GetProjectConnectionCount(projectId, connection.UserId) == 0)
                    await Clients.Group(ProjectGroup(projectId)).UserLeft(
                        new UserPresence(projectId, ToUser(connection, 0)));
                await BroadcastPresence(projectId);
            }
        }

        LastCursorTicks.TryRemove(Context.ConnectionId, out _);
        logger.LogInformation(
            "Collaboration connection {ConnectionId} closed for user {UserId}. Error: {Error}",
            Context.ConnectionId, connection?.UserId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinProject(Guid projectId)
    {
        await RequireProjectMember(projectId);
        var wasAlreadyPresent = presence.GetProjectConnectionCount(projectId, UserId) > 0;
        presence.JoinProject(Context.ConnectionId, projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId));

        var current = presence.GetProjectUsers(projectId).Single(user => user.UserId == UserId);
        if (!wasAlreadyPresent)
            await Clients.OthersInGroup(ProjectGroup(projectId))
                .UserJoined(new UserPresence(projectId, current));
        await BroadcastPresence(projectId);
    }

    public async Task LeaveProject(Guid projectId)
    {
        if (!presence.IsInProject(Context.ConnectionId, projectId))
            return;

        var current = presence.GetProjectUsers(projectId)
            .SingleOrDefault(user => user.UserId == UserId);
        presence.LeaveProject(Context.ConnectionId, projectId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId));
        if (presence.GetProjectConnectionCount(projectId, UserId) == 0)
            await Clients.Group(ProjectGroup(projectId))
                .UserLeft(new UserPresence(projectId, current is null
                    ? new CollaborationUser(UserId, "user", "User", null, 0, DateTime.UtcNow)
                    : current with { ConnectionCount = 0 }));
        await BroadcastPresence(projectId);
    }

    public async Task JoinFile(Guid fileId)
    {
        var projectId = await RequireFileMember(fileId);
        if (!presence.IsInProject(Context.ConnectionId, projectId))
            throw new HubException("Join the project before joining a file.");

        presence.JoinFile(Context.ConnectionId, fileId);
        await Groups.AddToGroupAsync(Context.ConnectionId, FileGroup(fileId));
        await InitializeLiveVersion(fileId);
    }

    public async Task LeaveFile(Guid fileId)
    {
        presence.LeaveFile(Context.ConnectionId, fileId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FileGroup(fileId));
        await Clients.OthersInGroup(FileGroup(fileId)).TypingStopped(fileId, UserId);
    }

    public async Task<CollaborativeStateMessage> JoinCollaborativeFile(Guid projectId, Guid fileId, string? stateVector = null)
    {
        await RequireCollaborativeFile(projectId, fileId);
        presence.JoinProject(Context.ConnectionId, projectId);
        presence.JoinFile(Context.ConnectionId, fileId);
        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId));
        await Groups.AddToGroupAsync(Context.ConnectionId, FileGroup(fileId));
        return await LoadCollaborativeState(fileId);
    }

    public async Task LeaveCollaborativeFile(Guid projectId, Guid fileId)
    {
        if (!presence.IsInFile(Context.ConnectionId, fileId)) return;
        presence.LeaveFile(Context.ConnectionId, fileId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FileGroup(fileId));
    }

    public async Task SendDocumentUpdate(CollaborativeUpdateMessage message)
    {
        if (message.UpdateId == Guid.Empty || string.IsNullOrWhiteSpace(message.EncodedUpdate)) throw new HubException("A valid Yjs update is required.");
        if (!presence.IsInFile(Context.ConnectionId, message.FileId)) throw new HubException("Join the collaborative file before sending updates.");
        await RequireCollaborativeFileWriter(message.ProjectId, message.FileId);
        byte[] update; try { update = Convert.FromBase64String(message.EncodedUpdate); } catch (FormatException) { throw new HubException("The Yjs update is not valid Base64."); }
        if (update.Length > 2_000_000) throw new HubException("The Yjs update exceeds the maximum size.");
        var appended = await documentStore.AppendUpdateAsync(message.ProjectId, message.FileId, message.UpdateId, update, UserId, Context.ConnectionAborted);
        if (!appended.Appended) return;
        var trusted = message with { ClientId = message.ClientId, CreatedAt = DateTime.UtcNow };
        await Clients.OthersInGroup(FileGroup(message.FileId)).DocumentUpdateReceived(trusted);
        if (message.PlainContent is not null) materializer.Enqueue(message.ProjectId, message.FileId, UserId, message.PlainContent);
    }

    public async Task<CollaborativeStateMessage> RequestDocumentState(Guid projectId, Guid fileId)
    {
        await RequireCollaborativeFile(projectId, fileId); return await LoadCollaborativeState(fileId);
    }

    public async Task SendAwarenessUpdate(CollaborativeUpdateMessage message)
    {
        if (!presence.IsInFile(Context.ConnectionId, message.FileId)) throw new HubException("Join the collaborative file before sending awareness.");
        await RequireCollaborativeFile(message.ProjectId, message.FileId);
        _ = Convert.FromBase64String(message.EncodedUpdate);
        await Clients.OthersInGroup(FileGroup(message.FileId)).AwarenessUpdateReceived(message with { CreatedAt = DateTime.UtcNow, PlainContent = null });
    }

    public async Task<long> SendCodeOperation(CodeOperation operation)
    {
        if (operation.FileId == Guid.Empty || operation.OperationId == Guid.Empty)
            throw new HubException("A valid operation and file identifier are required.");
        if (operation.UserId != Guid.Empty && operation.UserId != UserId)
            throw new HubException("The operation user does not match the authenticated user.");
        if (!presence.IsInFile(Context.ConnectionId, operation.FileId))
            throw new HubException("Join the file before sending operations.");

        await RequireFileWriter(operation.FileId);
        var serverVersion = await InitializeLiveVersion(operation.FileId);
        if (operation.BaseVersion != serverVersion)
        {
            await Clients.Caller.ResyncRequired(new ResyncRequiredMessage(
                operation.FileId, serverVersion, "The local base version is stale."));
            return -1;
        }

        var nextVersion = LiveVersions.AddOrUpdate(
            operation.FileId,
            _ => serverVersion + 1,
            (_, current) => current == serverVersion ? current + 1 : current);
        if (nextVersion != serverVersion + 1)
        {
            await Clients.Caller.ResyncRequired(new ResyncRequiredMessage(
                operation.FileId, nextVersion, "Another operation won the version race."));
            return -1;
        }

        var trustedOperation = operation with
        {
            UserId = UserId,
            ClientVersion = nextVersion,
            Timestamp = DateTime.UtcNow
        };
        await Clients.OthersInGroup(FileGroup(operation.FileId))
            .CodeOperationReceived(trustedOperation);
        await Clients.Caller.OperationAccepted(
            new OperationAcceptedMessage(operation.OperationId, operation.FileId, nextVersion));
        return nextVersion;
    }

    public async Task UpdateCursor(CursorPosition position)
    {
        if (!presence.IsInFile(Context.ConnectionId, position.FileId))
            throw new HubException("Join the file before updating a cursor.");
        if (position.LineNumber < 1 || position.Column < 1)
            throw new HubException("Cursor coordinates must be positive.");

        var now = DateTime.UtcNow.Ticks;
        var previous = LastCursorTicks.GetOrAdd(Context.ConnectionId, 0);
        if (new TimeSpan(now - previous) < CursorInterval)
            return;
        LastCursorTicks[Context.ConnectionId] = now;
        await Clients.OthersInGroup(FileGroup(position.FileId)).CursorUpdated(UserId, position);
    }

    public Task StartTyping(Guid fileId) => BroadcastTyping(fileId, true);
    public Task StopTyping(Guid fileId) => BroadcastTyping(fileId, false);

    public Task Heartbeat()
    {
        presence.Heartbeat(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public async Task JoinConversation(Guid conversationId)
    {
        if (!await db.ConversationParticipants.AsNoTracking().AnyAsync(item => item.ConversationId == conversationId && item.UserId == UserId))
            throw new HubException("You are not a participant in this conversation.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));

    public async Task JoinLiveRoom(Guid roomId)
    {
        var allowed = await db.LiveCodingRooms.AsNoTracking().AnyAsync(room => room.ID == roomId &&
            (room.OwnerId == UserId || room.Participants.Any(participant => participant.UserId == UserId && participant.Status != LiveRoomParticipantStatus.Removed) ||
             (room.Visibility == LiveRoomVisibility.ProjectMembers && room.ProjectId != null && db.ProjectMembers.Any(member => member.ProjectId == room.ProjectId && member.UserId == UserId))), Context.ConnectionAborted);
        if (!allowed) throw new HubException("You are not authorized to join this live room.");
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveRoomGroup(roomId));
    }

    public Task LeaveLiveRoom(Guid roomId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveRoomGroup(roomId));

    public async Task StartChatTyping(Guid conversationId)
    {
        if (!await db.ConversationParticipants.AsNoTracking().AnyAsync(item => item.ConversationId == conversationId && item.UserId == UserId))
            throw new HubException("You are not a participant in this conversation.");
        await Clients.OthersInGroup(ConversationGroup(conversationId)).ChatTypingStarted(conversationId, UserId);
    }

    public async Task StopChatTyping(Guid conversationId)
    {
        if (!await db.ConversationParticipants.AsNoTracking().AnyAsync(item => item.ConversationId == conversationId && item.UserId == UserId))
            return;
        await Clients.OthersInGroup(ConversationGroup(conversationId)).ChatTypingStopped(conversationId, UserId);
    }

    public async Task NotifyFileChanged(Guid fileId, int versionNumber, string concurrencyToken)
    {
        await RequireFileWriter(fileId);
        if (!presence.IsInFile(Context.ConnectionId, fileId))
            throw new HubException("Join the file before publishing a file change.");
        LiveVersions[fileId] = versionNumber;
        await Clients.OthersInGroup(FileGroup(fileId))
            .FileChanged(new FileChangedMessage(fileId, UserId, versionNumber, concurrencyToken));
    }

    private async Task BroadcastTyping(Guid fileId, bool started)
    {
        if (!presence.IsInFile(Context.ConnectionId, fileId))
            throw new HubException("Join the file before sending typing state.");
        await RequireFileMember(fileId);
        if (started)
            await Clients.OthersInGroup(FileGroup(fileId)).TypingStarted(fileId, UserId);
        else
            await Clients.OthersInGroup(FileGroup(fileId)).TypingStopped(fileId, UserId);
    }

    private async Task RequireProjectMember(Guid projectId)
    {
        var allowed = await db.ProjectMembers.AsNoTracking()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == UserId);
        if (!allowed)
            throw new HubException("You are not an active member of this project.");
    }

    private async Task<Guid> RequireFileMember(Guid fileId)
    {
        var projectId = await db.WorkspaceNodes.AsNoTracking()
            .Where(node => node.ID == fileId && node.NodeType == WorkspaceNodeType.File)
            .Select(node => (Guid?)node.ProjectId)
            .SingleOrDefaultAsync();
        if (projectId is null)
            throw new HubException("File not found.");
        await RequireProjectMember(projectId.Value);
        return projectId.Value;
    }

    private async Task RequireCollaborativeFile(Guid projectId, Guid fileId)
    {
        var exists = await db.WorkspaceNodes.AsNoTracking().AnyAsync(node => node.ID == fileId && node.ProjectId == projectId && node.NodeType == WorkspaceNodeType.File && !node.IsDeleted, Context.ConnectionAborted);
        if (!exists) throw new HubException("The file does not belong to the supplied project.");
        await RequireProjectMember(projectId);
    }

    private async Task RequireCollaborativeFileWriter(Guid projectId, Guid fileId)
    {
        var access = await db.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == UserId &&
                db.WorkspaceNodes.Any(node => node.ID == fileId && node.ProjectId == projectId &&
                    node.NodeType == WorkspaceNodeType.File && !node.IsDeleted))
            .Select(member => new { member.Role, member.Project.Status, member.Project.DeadlineAt })
            .SingleOrDefaultAsync(Context.ConnectionAborted);
        if (access is null)
            throw new HubException("The file does not belong to an accessible project.");
        if (!CollaborationAccessPolicy.CanWrite(access.Role, access.Status, access.DeadlineAt, DateTime.UtcNow))
            throw new HubException("This project role or lifecycle state has read-only workspace access.");
    }

    private async Task RequireFileWriter(Guid fileId)
    {
        var projectId = await db.WorkspaceNodes.AsNoTracking()
            .Where(node => node.ID == fileId && node.NodeType == WorkspaceNodeType.File && !node.IsDeleted)
            .Select(node => (Guid?)node.ProjectId)
            .SingleOrDefaultAsync(Context.ConnectionAborted);
        if (projectId is null) throw new HubException("File not found.");
        await RequireCollaborativeFileWriter(projectId.Value, fileId);
    }

    private async Task<CollaborativeStateMessage> LoadCollaborativeState(Guid fileId)
    {
        var snapshot = await documentStore.GetLatestSnapshotAsync(fileId, Context.ConnectionAborted); var after = snapshot?.SequenceNumber ?? 0;
        var updates = await documentStore.GetUpdatesAfterAsync(fileId, after, Context.ConnectionAborted);
        return new(snapshot is null ? null : Convert.ToBase64String(snapshot.EncodedState), updates.Select(x => new CollaborativeUpdateMessage(x.ProjectId, x.FileId, string.Empty, x.UpdateId, Convert.ToBase64String(x.EncodedUpdate), "document", x.CreatedAt)).ToArray(), updates.LastOrDefault()?.SequenceNumber ?? after);
    }

    private async Task<long> InitializeLiveVersion(Guid fileId)
    {
        if (LiveVersions.TryGetValue(fileId, out var version))
            return version;
        var persisted = await db.FileContents.AsNoTracking()
            .Where(content => content.NodeId == fileId)
            .Select(content => content.VersionNumber)
            .SingleOrDefaultAsync();
        return LiveVersions.GetOrAdd(fileId, persisted);
    }

    private Task BroadcastPresence(Guid projectId) =>
        Clients.Group(ProjectGroup(projectId))
            .PresenceUpdated(new PresenceUpdate(projectId, presence.GetProjectUsers(projectId)));

    private static CollaborationUser ToUser(ConnectionPresence connection, int connectionCount) =>
        new(connection.UserId, connection.UserName, connection.DisplayName, connection.AvatarUrl, connectionCount, connection.LastHeartbeat);

    public static string ProjectGroup(Guid projectId) => $"project:{projectId:N}";
    public static string FileGroup(Guid fileId) => $"file:{fileId:N}";
    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId:N}";
    public static string UserGroup(Guid userId) => $"user:{userId:N}";
    public static string LiveRoomGroup(Guid roomId) => $"live-room:{roomId:N}";
}
