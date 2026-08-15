using Coding.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/chat")]
public sealed class ChatController(ISender sender) : ControllerBase
{
    [HttpGet("conversations")]
    public Task<IReadOnlyList<ConversationItem>> Conversations(CancellationToken ct) => sender.Send(new GetUserConversationsQuery(), ct);
    [HttpPost("conversations/direct")]
    public Task<ConversationItem> Direct(CreateDirectConversationRequest request, CancellationToken ct) => sender.Send(new CreateDirectConversationCommand(request.OtherUserId), ct);
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public Task<CursorPage<ChatMessageItem>> Messages(Guid conversationId, [FromQuery] string? cursor, [FromQuery] int limit = 30, CancellationToken ct = default) => sender.Send(new GetConversationMessagesQuery(conversationId, cursor, limit), ct);
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public Task<ChatMessageItem> Send(Guid conversationId, SendChatMessageRequest request, CancellationToken ct) => sender.Send(new SendMessageCommand(conversationId, request.Content), ct);
    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> Read(Guid conversationId, MarkConversationReadRequest request, CancellationToken ct) { await sender.Send(new MarkConversationAsReadCommand(conversationId, request.ThroughMessageId), ct); return NoContent(); }
    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken ct) { await sender.Send(new DeleteOwnMessageCommand(messageId), ct); return NoContent(); }
    [HttpGet("unread")]
    public Task<IReadOnlyList<UnreadConversationCount>> Unread(CancellationToken ct) => sender.Send(new GetUnreadConversationCountsQuery(), ct);
}

public sealed record CreateDirectConversationRequest(string OtherUserId);
public sealed record SendChatMessageRequest(string Content);
public sealed record MarkConversationReadRequest(Guid? ThroughMessageId);
