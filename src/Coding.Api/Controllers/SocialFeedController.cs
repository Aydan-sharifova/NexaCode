using Coding.Application.Features.SocialFeed;
using Coding.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, EnableRateLimiting("social"), Route("api/feed")]
public sealed class SocialFeedController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<SocialPostPage> Feed([FromQuery] FeedTab tab = FeedTab.ForYou, [FromQuery] string? cursor = null, [FromQuery] int limit = 20, CancellationToken ct = default) =>
        sender.Send(new GetSocialFeedQuery(tab, cursor, limit), ct);

    [HttpGet("saved")]
    public Task<SocialPostPage> Saved([FromQuery] string? cursor = null, [FromQuery] int limit = 20, CancellationToken ct = default) =>
        sender.Send(new GetSavedPostsQuery(cursor, limit), ct);

    [HttpGet("discover")]
    public Task<SocialDiscover> Discover([FromQuery]string? search=null,[FromQuery]string? technology=null,[FromQuery]string? language=null,[FromQuery]string sort="Trending",[FromQuery]int limit=8,CancellationToken ct=default)=>sender.Send(new GetSocialDiscoverQuery(search,technology,language,sort,limit),ct);

    [HttpPost]
    public async Task<ActionResult<SocialPostItem>> Create(CreatePostRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(new CreateSocialPostCommand(request.Type, request.Content, request.CodeLanguage, request.ImageUrl, request.ProjectId), ct));

    [HttpPut("posts/{postId:guid}")]
    public Task<SocialPostItem> Update(Guid postId, UpdatePostRequest request, CancellationToken ct) =>
        sender.Send(new UpdateSocialPostCommand(postId, request.Content, request.CodeLanguage, request.ImageUrl), ct);

    [HttpDelete("posts/{postId:guid}")]
    public async Task<IActionResult> Delete(Guid postId, CancellationToken ct) { await sender.Send(new DeleteSocialPostCommand(postId), ct); return NoContent(); }

    [HttpPost("posts/{postId:guid}/like")]
    public Task<SocialToggleState> Like(Guid postId, CancellationToken ct) => sender.Send(new TogglePostLikeCommand(postId), ct);

    [HttpPost("posts/{postId:guid}/save")]
    public Task<SocialToggleState> Save(Guid postId, CancellationToken ct) => sender.Send(new TogglePostSaveCommand(postId), ct);

    [HttpPost("posts/{postId:guid}/share")]
    public Task<SocialToggleState> Share(Guid postId, CancellationToken ct) => sender.Send(new ShareSocialPostCommand(postId), ct);

    [HttpGet("posts/{postId:guid}/comments")]
    public Task<SocialCommentPage> Comments(Guid postId, [FromQuery] string? cursor = null, [FromQuery] int limit = 30, CancellationToken ct = default) =>
        sender.Send(new GetSocialCommentsQuery(postId, cursor, limit), ct);

    [HttpPost("posts/{postId:guid}/comments")]
    public async Task<ActionResult<SocialCommentItem>> Comment(Guid postId, CreateCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(new AddSocialCommentCommand(postId, request.ParentCommentId, request.Content), ct));

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct) { await sender.Send(new DeleteSocialCommentCommand(commentId), ct); return NoContent(); }
}

public sealed record CreatePostRequest(PostType Type, string Content, string? CodeLanguage, string? ImageUrl, Guid? ProjectId);
public sealed record UpdatePostRequest(string Content, string? CodeLanguage, string? ImageUrl);
public sealed record CreateCommentRequest(string Content, Guid? ParentCommentId = null);
