using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Chat;

public sealed record ChatUser(Guid Id, string PublicId, string UserName, string DisplayName, string? AvatarUrl);
public sealed record ConversationItem(Guid Id, string Type, Guid? ProjectId, string Name, IReadOnlyList<ChatUser> Participants, ChatMessageItem? LastMessage, int UnreadCount, DateTime UpdatedAt);
public sealed record ChatAttachmentItem(Guid Id, string FileName, string ContentType, long Size);
public sealed record ChatMessageItem(Guid Id, Guid ConversationId, ChatUser Sender, string Content, DateTime CreatedAt, DateTime? EditedAt, bool IsDeleted, IReadOnlyList<Guid> ReadByUserIds, IReadOnlyList<ChatAttachmentItem> Attachments);
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record UnreadConversationCount(Guid ConversationId, int Count);

public sealed record CreateDirectConversationCommand(string OtherUserId) : IRequest<ConversationItem>;
public sealed record SendMessageCommand(Guid ConversationId, string Content) : IRequest<ChatMessageItem>;
public sealed record MarkConversationAsReadCommand(Guid ConversationId, Guid? ThroughMessageId) : IRequest;
public sealed record DeleteOwnMessageCommand(Guid MessageId) : IRequest;
public sealed record EditOwnMessageCommand(Guid MessageId, string Content) : IRequest<ChatMessageItem>;
public sealed record DeleteConversationCommand(Guid ConversationId) : IRequest;
public sealed record SendMessageWithAttachmentCommand(Guid ConversationId, string? Content, string FileName, string ContentType, byte[] Bytes) : IRequest<ChatMessageItem>;
public sealed record GetChatAttachmentQuery(Guid AttachmentId) : IRequest<ChatAttachmentDownload>;
public sealed record ChatAttachmentDownload(string FileName, string ContentType, string StoredName);
public sealed record GetUserConversationsQuery : IRequest<IReadOnlyList<ConversationItem>>;
public sealed record GetConversationMessagesQuery(Guid ConversationId, string? Cursor, int Limit = 30) : IRequest<CursorPage<ChatMessageItem>>;
public sealed record GetUnreadConversationCountsQuery : IRequest<IReadOnlyList<UnreadConversationCount>>;

public sealed class CreateDirectConversationValidator : AbstractValidator<CreateDirectConversationCommand>
{
    public CreateDirectConversationValidator() => RuleFor(item => item.OtherUserId).NotEmpty().MaximumLength(254);
}
public sealed class EditOwnMessageValidator : AbstractValidator<EditOwnMessageCommand>
{
    public EditOwnMessageValidator() => RuleFor(item => item.Content).NotEmpty().MaximumLength(8000);
}
public sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(item => item.ConversationId).NotEmpty();
        RuleFor(item => item.Content).NotEmpty().MaximumLength(8000);
    }
}
public sealed class GetConversationMessagesValidator : AbstractValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesValidator() => RuleFor(item => item.Limit).InclusiveBetween(1, 100);
}

public interface IChatRealtimePublisher
{
    Task MessageReceivedAsync(ChatMessageItem message, IReadOnlyCollection<Guid> participantIds, CancellationToken cancellationToken);
    Task ConversationReadAsync(Guid conversationId, Guid userId, Guid? throughMessageId, DateTime readAt, IReadOnlyCollection<Guid> participantIds, CancellationToken cancellationToken);
    Task ConversationUpdatedAsync(Guid conversationId, IReadOnlyCollection<Guid> participantIds, CancellationToken cancellationToken);
}
