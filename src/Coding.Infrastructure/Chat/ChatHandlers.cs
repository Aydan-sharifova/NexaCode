using System.Text;
using System.Text.RegularExpressions;
using Coding.Application.Abstractions;
using Coding.Application.Features.Chat;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.Users;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Chat;

internal static partial class ChatSupport
{
    [GeneratedRegex(@"(?<![\w])@([A-Za-z0-9_.-]{2,50})", RegexOptions.CultureInvariant)]
    private static partial Regex MentionRegex();

    public static string DirectKey(Guid left, Guid right) =>
        string.CompareOrdinal(left.ToString("N"), right.ToString("N")) < 0
            ? $"{left:N}:{right:N}"
            : $"{right:N}:{left:N}";

    public static string EncodeCursor(DateTime createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt.Ticks}:{id:N}"));

    public static (DateTime CreatedAt, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            return parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParseExact(parts[1], "N", out var id)
                ? (new DateTime(ticks, DateTimeKind.Utc), id)
                : throw new FormatException();
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            throw new ValidationException("The pagination cursor is invalid.");
        }
    }

    public static async Task<ConversationParticipant> RequireParticipant(AppDbContext db, Guid conversationId, Guid userId, CancellationToken ct) =>
        await db.ConversationParticipants.SingleOrDefaultAsync(item => item.ConversationId == conversationId && item.UserId == userId, ct)
        ?? throw new ForbiddenException("You are not a participant in this conversation.");

    public static IQueryable<ChatMessageItem> ProjectMessages(IQueryable<ChatMessage> query) =>
        query.Select(message => new ChatMessageItem(
            message.ID,
            message.ConversationId,
            new ChatUser(message.SenderId, message.Sender.PublicId, message.Sender.UserName, message.Sender.FirstName + " " + message.Sender.LastName, message.Sender.AvatarUrl),
            message.IsDeleted ? "Message deleted" : message.Content,
            message.CreatedAt,
            message.EditedAtUtc,
            message.IsDeleted,
            message.ReadReceipts.Select(receipt => receipt.UserId).ToList(),
            message.IsDeleted ? new List<ChatAttachmentItem>() : message.Attachments.Select(attachment => new ChatAttachmentItem(attachment.ID, attachment.FileName, attachment.ContentType, attachment.Size)).ToList()));

    public static IReadOnlyCollection<string> Mentions(string content) =>
        MentionRegex().Matches(content).Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static async Task EnsureProjectChannels(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var missingProjects = await db.ProjectMembers
            .Where(member => member.UserId == userId && !db.Conversations.Any(conversation => conversation.ProjectId == member.ProjectId))
            .Select(member => new { member.ProjectId, member.Project.Name })
            .ToListAsync(ct);
        foreach (var project in missingProjects)
        {
            var channel = new Conversation { ID = Guid.NewGuid(), Type = ConversationType.ProjectChannel, ProjectId = project.ProjectId, Name = project.Name, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var memberIds = await db.ProjectMembers.Where(member => member.ProjectId == project.ProjectId).Select(member => member.UserId).ToListAsync(ct);
            foreach (var memberId in memberIds)
                channel.Participants.Add(new ConversationParticipant { ID = Guid.NewGuid(), UserId = memberId, JoinedAt = DateTime.UtcNow });
            db.Conversations.Add(channel);
        }
        if (missingProjects.Count > 0) await db.SaveChangesAsync(ct);
    }
}

public sealed class CreateDirectConversationHandler(AppDbContext db, ICurrentUser currentUser, ISocialAccessService socialAccess) : IRequestHandler<CreateDirectConversationCommand, ConversationItem>
{
    public async Task<ConversationItem> Handle(CreateDirectConversationCommand request, CancellationToken ct)
    {
        var identifier = request.OtherUserId.Trim().TrimStart('@');
        var otherUserId = await db.Users.AsNoTracking()
            .Where(user => !user.IsDeleted && !user.IsSuspended &&
                (user.PublicId == identifier.ToUpper() || EF.Functions.ILike(user.UserName, identifier) || EF.Functions.ILike(user.Email, identifier)))
            .Select(user => (Guid?)user.ID)
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");
        if (otherUserId == currentUser.UserId) throw new ConflictException("You cannot create a direct conversation with yourself.");
        await socialAccess.EnsureCanInteractAsync(currentUser.UserId, otherUserId, ct);
        var key = ChatSupport.DirectKey(currentUser.UserId, otherUserId);
        var existingId = await db.Conversations.Where(item => item.DirectKey == key).Select(item => (Guid?)item.ID).SingleOrDefaultAsync(ct);
        if (!existingId.HasValue)
        {
            var now = DateTime.UtcNow;
            var conversation = new Conversation { ID = Guid.NewGuid(), Type = ConversationType.Direct, DirectKey = key, CreatedAt = now, UpdatedAt = now };
            conversation.Participants.Add(new ConversationParticipant { ID = Guid.NewGuid(), UserId = currentUser.UserId, JoinedAt = now });
            conversation.Participants.Add(new ConversationParticipant { ID = Guid.NewGuid(), UserId = otherUserId, JoinedAt = now });
            db.Conversations.Add(conversation);
            try { await db.SaveChangesAsync(ct); existingId = conversation.ID; }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); existingId = await db.Conversations.Where(item => item.DirectKey == key).Select(item => (Guid?)item.ID).SingleAsync(ct); }
        }
        return (await ConversationProjection.LoadAsync(db, currentUser.UserId, existingId.Value, ct))!;
    }
}

public sealed class SendMessageHandler(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notifications,
    IChatRealtimePublisher realtime,
    ISocialAccessService socialAccess,
    ILogger<SendMessageHandler> logger) : IRequestHandler<SendMessageCommand, ChatMessageItem>
{
    public async Task<ChatMessageItem> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var participant = await ChatSupport.RequireParticipant(db, request.ConversationId, currentUser.UserId, ct);
        var conversation = await db.Conversations.AsNoTracking().SingleAsync(item => item.ID == participant.ConversationId, ct);
        var participantIds = await db.ConversationParticipants.Where(item => item.ConversationId == request.ConversationId).Select(item => item.UserId).ToListAsync(ct);
        if (conversation.Type == ConversationType.Direct)
            foreach (var targetId in participantIds.Where(id => id != currentUser.UserId))
                await socialAccess.EnsureCanInteractAsync(currentUser.UserId, targetId, ct);
        var sender = await db.Users.AsNoTracking().SingleAsync(item => item.ID == currentUser.UserId, ct);
        var mentionedNames = ChatSupport.Mentions(request.Content);
        var mentioned = mentionedNames.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(user => mentionedNames.Contains(user.UserName) && user.ID != currentUser.UserId).Select(user => new MentionedUser(user.ID, user.UserName)).ToListAsync(ct);
        if (mentioned.Count != mentionedNames.Count) throw new ValidationException("One or more mentioned users do not exist.");
        if (conversation.ProjectId.HasValue)
        {
            var mentionedIds = mentioned.Select(user => user.Id).ToArray();
            var allowedCount = await db.ProjectMembers.CountAsync(member => member.ProjectId == conversation.ProjectId && mentionedIds.Contains(member.UserId), ct);
            if (allowedCount != mentioned.Count) throw new ForbiddenException("A mentioned user does not have access to this project.");
        }
        else if (mentioned.Any(user => !participantIds.Contains(user.Id))) throw new ForbiddenException("A mentioned user is not part of this conversation.");
        var now = DateTime.UtcNow;
        var message = new ChatMessage { ID = Guid.NewGuid(), ConversationId = request.ConversationId, SenderId = currentUser.UserId, Content = request.Content.Trim(), CreatedAt = now, CreatAt = now };
        db.ChatMessages.Add(message);
        db.MessageReadReceipts.Add(new MessageReadReceipt { Message = message, UserId = currentUser.UserId, ReadAt = now });
        var trackedConversation = await db.Conversations.SingleAsync(item => item.ID == request.ConversationId, ct);
        trackedConversation.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var dto = new ChatMessageItem(message.ID, message.ConversationId, new ChatUser(sender.ID, sender.PublicId, sender.UserName, sender.FirstName + " " + sender.LastName, sender.AvatarUrl), message.Content, now, null, false, [currentUser.UserId], []);
        var notificationRequests = new List<CreateNotificationRequest>();
        if (conversation.Type == ConversationType.Direct)
            notificationRequests.AddRange(participantIds.Where(id => id != currentUser.UserId).Select(id =>
                new CreateNotificationRequest(id, NotificationType.DirectMessage, sender.UserName,
                    message.Content, request.ConversationId, nameof(Conversation))));

        if (mentioned.Count > 0)
        {
            notificationRequests.AddRange(mentioned.Select(user => new CreateNotificationRequest(user.Id, NotificationType.UserMention, $"{sender.UserName} mentioned you", message.Content, message.ID, nameof(ChatMessage))));
        }

        try
        {
            if (notificationRequests.Count > 0)
                await notifications.CreateManyAsync(
                    notificationRequests.DistinctBy(item => (item.UserId, item.Type)), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error,
                "Message {MessageId} was saved, but notifications could not be delivered.",
                message.ID);
        }

        try
        {
            await realtime.MessageReceivedAsync(dto, participantIds, ct);
            await realtime.ConversationUpdatedAsync(request.ConversationId, participantIds, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error,
                "Message {MessageId} was saved, but its real-time event could not be delivered.",
                message.ID);
        }
        return dto;
    }
}

internal sealed record MentionedUser(Guid Id, string UserName);

public sealed class MarkConversationAsReadHandler(AppDbContext db, ICurrentUser currentUser, IChatRealtimePublisher realtime, INotificationService notifications) : IRequestHandler<MarkConversationAsReadCommand>
{
    public async Task Handle(MarkConversationAsReadCommand request, CancellationToken ct)
    {
        var participant = await ChatSupport.RequireParticipant(db, request.ConversationId, currentUser.UserId, ct);
        var through = request.ThroughMessageId.HasValue
            ? await db.ChatMessages.SingleOrDefaultAsync(item => item.ID == request.ThroughMessageId && item.ConversationId == request.ConversationId, ct) ?? throw new NotFoundException("Message not found.")
            : await db.ChatMessages.Where(item => item.ConversationId == request.ConversationId).OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.ID).FirstOrDefaultAsync(ct);
        if (through is null) return;
        var unreadMessageIds = await db.ChatMessages.Where(item => item.ConversationId == request.ConversationId && item.SenderId != currentUser.UserId && (item.CreatedAt < through.CreatedAt || item.CreatedAt == through.CreatedAt && item.ID.CompareTo(through.ID) <= 0) && !item.ReadReceipts.Any(receipt => receipt.UserId == currentUser.UserId)).Select(item => item.ID).ToListAsync(ct);
        var now = DateTime.UtcNow;
        db.MessageReadReceipts.AddRange(unreadMessageIds.Select(id => new MessageReadReceipt { MessageId = id, UserId = currentUser.UserId, ReadAt = now }));
        participant.LastReadAt = now; participant.LastReadMessageId = through.ID;
        await db.SaveChangesAsync(ct);
        await notifications.MarkRelatedReadAsync(currentUser.UserId, NotificationType.DirectMessage, request.ConversationId, ct);
        var ids = await db.ConversationParticipants.Where(item => item.ConversationId == request.ConversationId).Select(item => item.UserId).ToListAsync(ct);
        await realtime.ConversationReadAsync(request.ConversationId, currentUser.UserId, through.ID, now, ids, ct);
    }
}

public sealed class DeleteOwnMessageHandler(AppDbContext db, ICurrentUser currentUser, IChatRealtimePublisher realtime) : IRequestHandler<DeleteOwnMessageCommand>
{
    public async Task Handle(DeleteOwnMessageCommand request, CancellationToken ct)
    {
        var message = await db.ChatMessages.SingleOrDefaultAsync(item => item.ID == request.MessageId, ct) ?? throw new NotFoundException("Message not found.");
        if (message.SenderId != currentUser.UserId) throw new ForbiddenException("You can delete only your own messages.");
        message.IsDeleted = true; message.DeletedAtUtc = message.DeletedAt = message.UpdateAt = DateTime.UtcNow; message.Content = string.Empty;
        await db.SaveChangesAsync(ct);
        var ids = await db.ConversationParticipants.Where(item => item.ConversationId == message.ConversationId).Select(item => item.UserId).ToListAsync(ct);
        await realtime.ConversationUpdatedAsync(message.ConversationId, ids, ct);
    }
}

public sealed class EditOwnMessageHandler(AppDbContext db, ICurrentUser currentUser, IChatRealtimePublisher realtime) : IRequestHandler<EditOwnMessageCommand, ChatMessageItem>
{
    public async Task<ChatMessageItem> Handle(EditOwnMessageCommand request, CancellationToken ct)
    {
        var message = await db.ChatMessages.SingleOrDefaultAsync(item => item.ID == request.MessageId, ct) ?? throw new NotFoundException("Message not found.");
        if (message.SenderId != currentUser.UserId) throw new ForbiddenException("You can edit only your own messages.");
        if (message.IsDeleted) throw new ConflictException("A deleted message cannot be edited.");
        message.Content = request.Content.Trim(); message.EditedAtUtc = message.UpdateAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var ids = await db.ConversationParticipants.Where(item => item.ConversationId == message.ConversationId).Select(item => item.UserId).ToListAsync(ct);
        await realtime.ConversationUpdatedAsync(message.ConversationId, ids, ct);
        return await ChatSupport.ProjectMessages(db.ChatMessages.AsNoTracking().Where(item => item.ID == message.ID)).SingleAsync(ct);
    }
}

public sealed class DeleteConversationHandler(AppDbContext db, ICurrentUser currentUser, IChatRealtimePublisher realtime) : IRequestHandler<DeleteConversationCommand>
{
    public async Task Handle(DeleteConversationCommand request, CancellationToken ct)
    {
        await ChatSupport.RequireParticipant(db, request.ConversationId, currentUser.UserId, ct);
        var conversation = await db.Conversations.SingleOrDefaultAsync(item => item.ID == request.ConversationId, ct) ?? throw new NotFoundException("Conversation not found.");
        if (conversation.Type != ConversationType.Direct) throw new ForbiddenException("Project channels cannot be deleted from chat.");
        var ids = await db.ConversationParticipants.Where(item => item.ConversationId == request.ConversationId).Select(item => item.UserId).ToListAsync(ct);
        var storedNames = await db.ChatAttachments.IgnoreQueryFilters().Where(item => item.Message.ConversationId == request.ConversationId).Select(item => item.StoredName).ToListAsync(ct);
        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync(ct);
        var root = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "chat-attachments");
        foreach (var storedName in storedNames)
        {
            var path = Path.Combine(root, storedName);
            if (File.Exists(path)) File.Delete(path);
        }
        await realtime.ConversationUpdatedAsync(request.ConversationId, ids, ct);
    }
}

public sealed class SendMessageWithAttachmentHandler(AppDbContext db, ICurrentUser currentUser, IChatRealtimePublisher realtime, ISocialAccessService socialAccess) : IRequestHandler<SendMessageWithAttachmentCommand, ChatMessageItem>
{
    private const int MaxBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".app", ".bat", ".cmd", ".com", ".dmg", ".exe", ".js", ".msi", ".pkg", ".ps1", ".scr", ".vbs" };

    public async Task<ChatMessageItem> Handle(SendMessageWithAttachmentCommand request, CancellationToken ct)
    {
        await ChatSupport.RequireParticipant(db, request.ConversationId, currentUser.UserId, ct);
        var directTargets = await db.ConversationParticipants.AsNoTracking()
            .Where(item => item.ConversationId == request.ConversationId && item.UserId != currentUser.UserId && item.Conversation.Type == ConversationType.Direct)
            .Select(item => item.UserId).ToListAsync(ct);
        foreach (var targetId in directTargets) await socialAccess.EnsureCanInteractAsync(currentUser.UserId, targetId, ct);
        if (request.Bytes.Length is 0 or > MaxBytes) throw new ValidationException("Attachments must be between 1 byte and 10 MB.");
        var fileName = Path.GetFileName(request.FileName).Trim();
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || BlockedExtensions.Contains(Path.GetExtension(fileName))) throw new ValidationException("This attachment name or file type is not allowed.");
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var root = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "chat-attachments");
        Directory.CreateDirectory(root);
        var fullPath = Path.Combine(root, storedName);
        await File.WriteAllBytesAsync(fullPath, request.Bytes, ct);
        try
        {
            var now = DateTime.UtcNow;
            var message = new ChatMessage { ID = Guid.NewGuid(), ConversationId = request.ConversationId, SenderId = currentUser.UserId, Content = string.IsNullOrWhiteSpace(request.Content) ? fileName : request.Content.Trim(), CreatedAt = now, CreatAt = now };
            var attachment = new ChatAttachment { ID = Guid.NewGuid(), Message = message, UploadedById = currentUser.UserId, FileName = fileName, StoredName = storedName, ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType[..Math.Min(120, request.ContentType.Length)], Size = request.Bytes.LongLength, CreatAt = now };
            message.Attachments.Add(attachment);
            db.ChatMessages.Add(message);
            db.MessageReadReceipts.Add(new MessageReadReceipt { Message = message, UserId = currentUser.UserId, ReadAt = now });
            var conversation = await db.Conversations.SingleAsync(item => item.ID == request.ConversationId, ct); conversation.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            var dto = await ChatSupport.ProjectMessages(db.ChatMessages.AsNoTracking().Where(item => item.ID == message.ID)).SingleAsync(ct);
            var ids = await db.ConversationParticipants.Where(item => item.ConversationId == request.ConversationId).Select(item => item.UserId).ToListAsync(ct);
            await realtime.MessageReceivedAsync(dto, ids, ct); await realtime.ConversationUpdatedAsync(request.ConversationId, ids, ct);
            return dto;
        }
        catch { File.Delete(fullPath); throw; }
    }
}

public sealed class GetChatAttachmentHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetChatAttachmentQuery, ChatAttachmentDownload>
{
    public async Task<ChatAttachmentDownload> Handle(GetChatAttachmentQuery request, CancellationToken ct)
    {
        var attachment = await db.ChatAttachments.AsNoTracking().Where(item => item.ID == request.AttachmentId && !item.Message.IsDeleted)
            .Select(item => new { item.FileName, item.ContentType, item.StoredName, item.Message.ConversationId }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Attachment not found.");
        await ChatSupport.RequireParticipant(db, attachment.ConversationId, currentUser.UserId, ct);
        return new(attachment.FileName, attachment.ContentType, attachment.StoredName);
    }
}

public sealed class GetUserConversationsHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetUserConversationsQuery, IReadOnlyList<ConversationItem>>
{
    public async Task<IReadOnlyList<ConversationItem>> Handle(GetUserConversationsQuery request, CancellationToken ct)
    {
        await ChatSupport.EnsureProjectChannels(db, currentUser.UserId, ct);
        return await ConversationProjection.LoadAllAsync(db, currentUser.UserId, ct);
    }
}

public sealed class GetConversationMessagesHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetConversationMessagesQuery, CursorPage<ChatMessageItem>>
{
    public async Task<CursorPage<ChatMessageItem>> Handle(GetConversationMessagesQuery request, CancellationToken ct)
    {
        await ChatSupport.RequireParticipant(db, request.ConversationId, currentUser.UserId, ct);
        var cursor = ChatSupport.DecodeCursor(request.Cursor);
        var query = db.ChatMessages.AsNoTracking().Where(item => item.ConversationId == request.ConversationId);
        if (cursor.HasValue) query = query.Where(item => item.CreatedAt < cursor.Value.CreatedAt || item.CreatedAt == cursor.Value.CreatedAt && item.ID.CompareTo(cursor.Value.Id) < 0);
        var messages = await ChatSupport.ProjectMessages(query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.ID)).Take(request.Limit + 1).ToListAsync(ct);
        var hasMore = messages.Count > request.Limit;
        if (hasMore) messages.RemoveAt(messages.Count - 1);
        var last = messages.LastOrDefault();
        return new CursorPage<ChatMessageItem>(messages, hasMore && last is not null ? ChatSupport.EncodeCursor(last.CreatedAt, last.Id) : null);
    }
}

public sealed class GetUnreadConversationCountsHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetUnreadConversationCountsQuery, IReadOnlyList<UnreadConversationCount>>
{
    public async Task<IReadOnlyList<UnreadConversationCount>> Handle(GetUnreadConversationCountsQuery request, CancellationToken ct) =>
        await db.ConversationParticipants
            .Where(participant => participant.UserId == currentUser.UserId &&
                (participant.Conversation.Type != ConversationType.Direct ||
                 !participant.Conversation.Participants.Any(other =>
                     other.UserId != currentUser.UserId &&
                     db.UserBlocks.Any(block =>
                         block.BlockerId == currentUser.UserId && block.BlockedId == other.UserId ||
                         block.BlockerId == other.UserId && block.BlockedId == currentUser.UserId))))
            .Select(participant => new UnreadConversationCount(participant.ConversationId, participant.Conversation.ChatMessages.Count(message => message.SenderId != currentUser.UserId && !message.ReadReceipts.Any(receipt => receipt.UserId == currentUser.UserId)))).ToListAsync(ct);
}

internal static class ConversationProjection
{
    public static async Task<IReadOnlyList<ConversationItem>> LoadAllAsync(
        AppDbContext db, Guid userId, CancellationToken ct)
    {
        var conversations = await db.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.UserId == userId &&
                (participant.Conversation.Type != ConversationType.Direct ||
                 !participant.Conversation.Participants.Any(other =>
                     other.UserId != userId &&
                     db.UserBlocks.Any(block =>
                         block.BlockerId == userId && block.BlockedId == other.UserId ||
                         block.BlockerId == other.UserId && block.BlockedId == userId))))
            .Select(participant => new
            {
                participant.ConversationId,
                participant.Conversation.Type,
                participant.Conversation.ProjectId,
                participant.Conversation.Name,
                participant.Conversation.UpdatedAt
            })
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(ct);

        var items = new List<ConversationItem>(conversations.Count);
        foreach (var conversation in conversations)
            items.Add(await BuildAsync(db, userId, conversation.ConversationId, conversation.Type,
                conversation.ProjectId, conversation.Name, conversation.UpdatedAt, ct));
        return items;
    }

    public static async Task<ConversationItem?> LoadAsync(
        AppDbContext db, Guid userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await db.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.UserId == userId && participant.ConversationId == conversationId)
            .Select(participant => new
            {
                participant.ConversationId,
                participant.Conversation.Type,
                participant.Conversation.ProjectId,
                participant.Conversation.Name,
                participant.Conversation.UpdatedAt
            })
            .SingleOrDefaultAsync(ct);
        return conversation is null
            ? null
            : await BuildAsync(db, userId, conversation.ConversationId, conversation.Type,
                conversation.ProjectId, conversation.Name, conversation.UpdatedAt, ct);
    }

    private static async Task<ConversationItem> BuildAsync(
        AppDbContext db,
        Guid userId,
        Guid conversationId,
        ConversationType type,
        Guid? projectId,
        string? channelName,
        DateTime updatedAt,
        CancellationToken ct)
    {
        var participants = await db.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.ConversationId == conversationId)
            .OrderBy(participant => participant.JoinedAt)
            .Select(participant => new ChatUser(
                participant.UserId,
                participant.User.PublicId,
                participant.User.UserName,
                participant.User.FirstName + " " + participant.User.LastName,
                participant.User.AvatarUrl))
            .ToListAsync(ct);

        var lastMessage = await ChatSupport.ProjectMessages(db.ChatMessages.AsNoTracking()
                .Where(message => message.ConversationId == conversationId)
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.ID))
            .FirstOrDefaultAsync(ct);

        var unreadCount = await db.ChatMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId &&
                message.SenderId != userId &&
                !message.ReadReceipts.Any(receipt => receipt.UserId == userId))
            .CountAsync(ct);

        var name = type == ConversationType.ProjectChannel
            ? channelName ?? "Project channel"
            : participants.FirstOrDefault(participant => participant.Id != userId)?.DisplayName
                ?? "Direct conversation";

        return new ConversationItem(conversationId, type.ToString(), projectId, name,
            participants, lastMessage, unreadCount, updatedAt);
    }
}
