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
    [HttpPut("messages/{messageId:guid}")]
    public Task<ChatMessageItem> Edit(Guid messageId, SendChatMessageRequest request, CancellationToken ct) => sender.Send(new EditOwnMessageCommand(messageId, request.Content), ct);
    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct) { await sender.Send(new DeleteConversationCommand(conversationId), ct); return NoContent(); }
    [HttpPost("conversations/{conversationId:guid}/attachments"), RequestSizeLimit(10_485_760)]
    public async Task<ChatMessageItem> Upload(Guid conversationId, [FromForm] IFormFile file, [FromForm] string? content, CancellationToken ct)
    {
        if (file.Length == 0) throw new BadHttpRequestException("The attachment is empty.");
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return await sender.Send(new SendMessageWithAttachmentCommand(conversationId, content, file.FileName, file.ContentType, memory.ToArray()), ct);
    }
    [HttpGet("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Attachment(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await sender.Send(new GetChatAttachmentQuery(attachmentId), ct);
        var root = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "chat-attachments");
        var path = Path.Combine(root, attachment.StoredName);
        return PhysicalFile(path, attachment.ContentType, attachment.FileName, enableRangeProcessing: true);
    }
    [HttpGet("unread")]
    public Task<IReadOnlyList<UnreadConversationCount>> Unread(CancellationToken ct) => sender.Send(new GetUnreadConversationCountsQuery(), ct);
}

public sealed record CreateDirectConversationRequest(string OtherUserId);
public sealed record SendChatMessageRequest(string Content);
public sealed record MarkConversationReadRequest(Guid? ThroughMessageId);
