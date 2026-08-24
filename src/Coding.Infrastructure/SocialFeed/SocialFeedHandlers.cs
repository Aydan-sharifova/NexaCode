using System.Text;
using System.Text.RegularExpressions;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.SocialFeed;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using Coding.Infrastructure.Projects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.SocialFeed;

internal static partial class SocialFeedSupport
{
    internal sealed record Cursor(int? Rank, DateTime CreatedAt, Guid Id);
    internal sealed class PostRow
    {
        public Guid Id { get; init; }
        public PostType Type { get; init; }
        public required string Content { get; init; }
        public string? CodeLanguage { get; init; }
        public string? ImageUrl { get; init; }
        public Guid AuthorId { get; init; }
        public required string PublicId { get; init; }
        public required string UserName { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? AvatarUrl { get; init; }
        public Guid? ProjectId { get; init; }
        public string? ProjectName { get; init; }
        public int Likes { get; init; }
        public int Comments { get; init; }
        public int Saves { get; init; }
        public int Shares { get; init; }
        public bool IsLiked { get; init; }
        public bool IsSaved { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public int Rank { get; init; }
    }

    internal static string Encode(Cursor cursor) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        $"{cursor.Rank?.ToString() ?? "_"}:{cursor.CreatedAt.Ticks}:{cursor.Id:N}"));

    internal static Cursor? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split(':');
            if (parts.Length != 3 || !long.TryParse(parts[1], out var ticks) || !Guid.TryParseExact(parts[2], "N", out var id)) throw new FormatException();
            int? rank = parts[0] == "_" ? null : int.Parse(parts[0]);
            return new(rank, new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            throw new FluentValidation.ValidationException("The feed cursor is invalid.");
        }
    }

    internal static IQueryable<PostRow> Rows(IQueryable<SocialPost> posts, Guid userId) => posts.Select(post => new PostRow
    {
        Id = post.ID, Type = post.Type, Content = post.Content, CodeLanguage = post.CodeLanguage, ImageUrl = post.ImageUrl,
        AuthorId = post.AuthorId, PublicId = post.Author.PublicId, UserName = post.Author.UserName,
        FirstName = post.Author.FirstName, LastName = post.Author.LastName, AvatarUrl = post.Author.AvatarUrl,
        ProjectId = post.ProjectId, ProjectName = post.Project == null ? null : post.Project.Name,
        Likes = post.Reactions.Count, Comments = post.Comments.Count, Saves = post.Saves.Count, Shares = post.Shares.Count,
        IsLiked = post.Reactions.Any(item => item.UserId == userId), IsSaved = post.Saves.Any(item => item.UserId == userId),
        CreatedAt = post.CreatedAt, UpdatedAt = post.UpdatedAt,
        Rank = post.Reactions.Count * 3 + post.Comments.Count * 2 + post.Saves.Count * 4 + post.Shares.Count * 3
    });

    internal static IQueryable<SocialPost> VisiblePosts(AppDbContext db, Guid userId) => db.SocialPosts.Where(post =>
        post.AuthorId == userId || !db.UserBlocks.Any(block =>
            block.BlockerId == userId && block.BlockedId == post.AuthorId ||
            block.BlockerId == post.AuthorId && block.BlockedId == userId));

    internal static SocialPostItem Item(PostRow row, Guid userId) => new(
        row.Id, row.Type, row.Content, row.CodeLanguage, row.ImageUrl,
        new(row.AuthorId, row.PublicId, row.UserName, $"{row.FirstName} {row.LastName}".Trim(), row.AvatarUrl),
        row.ProjectId.HasValue ? new(row.ProjectId.Value, row.ProjectName ?? "Project") : null,
        row.Likes, row.Comments, row.Saves, row.Shares, row.IsLiked, row.IsSaved, row.AuthorId == userId,
        row.CreatedAt, row.UpdatedAt);

    internal static async Task<PostRow> RequirePostRow(AppDbContext db, Guid postId, Guid userId, CancellationToken ct) =>
        await Rows(VisiblePosts(db, userId).AsNoTracking().Where(item => item.ID == postId), userId).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Post not found.");

    internal static async Task NotifyMentions(AppDbContext db, INotificationService notifications, Guid actorId, string actorName, Guid postId, string content, CancellationToken ct)
    {
        var names = MentionRegex().Matches(content).Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
        if (names.Length == 0) return;
        var users = await db.Users.AsNoTracking().Where(user => user.ID != actorId && names.Contains(user.UserName) &&
            !db.UserBlocks.Any(block => block.BlockerId == actorId && block.BlockedId == user.ID || block.BlockerId == user.ID && block.BlockedId == actorId)).Select(user => user.ID).ToListAsync(ct);
        await notifications.CreateManyAsync(users.Select(userId => new CreateNotificationRequest(
            userId, NotificationType.PostMention, "You were mentioned", $"{actorName} mentioned you in a post.", postId, nameof(SocialPost))), ct);
    }

    [GeneratedRegex(@"(?<![\w])@([A-Za-z0-9_.-]{3,50})", RegexOptions.CultureInvariant)]
    private static partial Regex MentionRegex();
}

public sealed class GetSocialFeedHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetSocialFeedQuery, SocialPostPage>
{
    public async Task<SocialPostPage> Handle(GetSocialFeedQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);
        var cursor = SocialFeedSupport.Decode(request.Cursor);
        var posts = SocialFeedSupport.VisiblePosts(db, user.UserId).AsNoTracking();
        if (request.Tab == FeedTab.Following)
            posts = posts.Where(post => db.UserFollows.Any(follow => follow.FollowerId == user.UserId && follow.FollowingId == post.AuthorId));
        var rows = SocialFeedSupport.Rows(posts, user.UserId);
        if (request.Tab == FeedTab.Trending)
        {
            rows = rows.Where(row => row.CreatedAt >= DateTime.UtcNow.AddDays(-30));
            if (cursor?.Rank is int rank)
                rows = rows.Where(row => row.Rank < rank || row.Rank == rank && (row.CreatedAt < cursor.CreatedAt || row.CreatedAt == cursor.CreatedAt && row.Id.CompareTo(cursor.Id) < 0));
            rows = rows.OrderByDescending(row => row.Rank).ThenByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id);
        }
        else
        {
            if (cursor is not null)
                rows = rows.Where(row => row.CreatedAt < cursor.CreatedAt || row.CreatedAt == cursor.CreatedAt && row.Id.CompareTo(cursor.Id) < 0);
            rows = rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id);
        }
        var page = await rows.Take(limit + 1).ToListAsync(ct);
        var hasMore = page.Count > limit;
        if (hasMore) page.RemoveAt(page.Count - 1);
        var last = page.LastOrDefault();
        return new(page.Select(row => SocialFeedSupport.Item(row, user.UserId)).ToArray(), hasMore && last is not null
            ? SocialFeedSupport.Encode(new(request.Tab == FeedTab.Trending ? last.Rank : null, last.CreatedAt, last.Id)) : null);
    }
}

public sealed class GetSavedPostsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetSavedPostsQuery, SocialPostPage>
{
    public async Task<SocialPostPage> Handle(GetSavedPostsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);
        var cursor = SocialFeedSupport.Decode(request.Cursor);
        var posts = SocialFeedSupport.VisiblePosts(db, user.UserId).AsNoTracking().Where(post => post.Saves.Any(save => save.UserId == user.UserId));
        var rows = SocialFeedSupport.Rows(posts, user.UserId);
        if (cursor is not null) rows = rows.Where(row => row.CreatedAt < cursor.CreatedAt || row.CreatedAt == cursor.CreatedAt && row.Id.CompareTo(cursor.Id) < 0);
        var page = await rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id).Take(limit + 1).ToListAsync(ct);
        var hasMore = page.Count > limit;
        if (hasMore) page.RemoveAt(page.Count - 1);
        var last = page.LastOrDefault();
        return new(page.Select(row => SocialFeedSupport.Item(row, user.UserId)).ToArray(), hasMore && last is not null ? SocialFeedSupport.Encode(new(null, last.CreatedAt, last.Id)) : null);
    }
}

public sealed class GetSocialDiscoverHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetSocialDiscoverQuery, SocialDiscover>
{
    public async Task<SocialDiscover> Handle(GetSocialDiscoverQuery request,CancellationToken ct)
    {
        var limit=Math.Clamp(request.Limit,1,20);
        var search=request.Search?.Trim(); var technology=request.Technology?.Trim(); var language=request.Language?.Trim();
        var sort=request.Sort.Trim().ToLowerInvariant();
        if(sort is not ("popularity" or "recent" or "trending")) sort="trending";
        var blocked=db.UserBlocks.Where(b=>b.BlockerId==user.UserId||b.BlockedId==user.UserId);
        var visibleUsers=db.Users.AsNoTracking().Where(x=>x.ID!=user.UserId&&!x.IsDeleted&&!x.IsSuspended&&
            !db.UserBlocks.Any(b=>b.BlockerId==user.UserId&&b.BlockedId==x.ID||b.BlockerId==x.ID&&b.BlockedId==user.UserId));
        if(!string.IsNullOrWhiteSpace(search)) visibleUsers=visibleUsers.Where(x=>x.UserName.Contains(search)||x.FirstName.Contains(search)||x.LastName.Contains(search)||x.Bio!=null&&x.Bio.Contains(search));
        if(!string.IsNullOrWhiteSpace(technology)) visibleUsers=visibleUsers.Where(x=>x.DeveloperProfile!=null&&x.DeveloperProfile.Skills.Contains(technology));
        var developerRows=await visibleUsers.Select(x=>new{x.ID,x.PublicId,x.UserName,x.FirstName,x.LastName,x.AvatarUrl,
            Followers=db.UserFollows.Count(f=>f.FollowingId==x.ID),Posts=db.SocialPosts.Count(p=>p.AuthorId==x.ID)})
            .OrderByDescending(x=>x.Followers).ThenByDescending(x=>x.Posts).ThenBy(x=>x.UserName).Take(limit).ToListAsync(ct);
        var developers=developerRows.Select(x=>new DiscoverDeveloper(x.ID,x.PublicId,x.UserName,(x.FirstName+" "+x.LastName).Trim(),x.AvatarUrl,x.Followers,x.Posts)).ToList();
        var projectQuery=db.Projects.AsNoTracking().Where(x=>x.IsPublic&&!blocked.Any(b=>b.BlockerId==x.OwnerId||b.BlockedId==x.OwnerId));
        if(!string.IsNullOrWhiteSpace(search)) projectQuery=projectQuery.Where(x=>x.Name.Contains(search)||x.Description!=null&&x.Description.Contains(search));
        if(!string.IsNullOrWhiteSpace(language)) projectQuery=projectQuery.Where(x=>x.DefaultLanguage==language);
        if(!string.IsNullOrWhiteSpace(technology)) projectQuery=projectQuery.Where(x=>x.DefaultLanguage.Contains(technology)||x.Description!=null&&x.Description.Contains(technology));
        var projectRows=await projectQuery.Select(x=>new{x.ID,x.Name,x.Description,OwnerPublicId=x.Owner.PublicId,
            Saves=db.SavedSocialPosts.Count(s=>s.Post.ProjectId==x.ID)}).OrderByDescending(x=>x.Saves).ThenBy(x=>x.Name).Take(limit).ToListAsync(ct);
        var projects=projectRows.Select(x=>new DiscoverProject(x.ID,x.Name,x.Description,x.OwnerPublicId,x.Saves)).ToList();
        var snippetQuery=SocialFeedSupport.VisiblePosts(db,user.UserId).AsNoTracking().Where(x=>x.Type==PostType.Code&&x.CodeLanguage!=null);
        if(!string.IsNullOrWhiteSpace(search)) snippetQuery=snippetQuery.Where(x=>x.Content.Contains(search));
        if(!string.IsNullOrWhiteSpace(language)) snippetQuery=snippetQuery.Where(x=>x.CodeLanguage==language);
        if(!string.IsNullOrWhiteSpace(technology)) snippetQuery=snippetQuery.Where(x=>x.CodeLanguage!.Contains(technology)||x.Content.Contains(technology));
        var snippetsBase=snippetQuery.Select(x=>new{x.ID,x.Content,Language=x.CodeLanguage!,AuthorId=x.Author.ID,x.Author.PublicId,x.Author.UserName,x.Author.FirstName,x.Author.LastName,x.Author.AvatarUrl,Likes=x.Reactions.Count,Saves=x.Saves.Count,x.CreatedAt});
        var snippetRows=sort=="recent"?await snippetsBase.OrderByDescending(x=>x.CreatedAt).Take(limit).ToListAsync(ct):await snippetsBase.OrderByDescending(x=>x.Saves).ThenByDescending(x=>x.Likes).ThenByDescending(x=>x.CreatedAt).Take(limit).ToListAsync(ct);
        var snippets=snippetRows.Select(x=>new DiscoverSnippet(x.ID,x.Content,x.Language,new FeedAuthor(x.AuthorId,x.PublicId,x.UserName,(x.FirstName+" "+x.LastName).Trim(),x.AvatarUrl),x.Likes,x.Saves,x.CreatedAt)).ToList();
        var topicRows=await snippetQuery.GroupBy(x=>x.CodeLanguage!).Select(x=>new{Name=x.Key,Posts=x.Count()}).OrderByDescending(x=>x.Posts).ThenBy(x=>x.Name).Take(limit).ToListAsync(ct);
        var topics=topicRows.Select(x=>new DiscoverTopic(x.Name,x.Posts)).ToList();
        async Task<List<DiscoverPackage>> Packages(MarketplaceCategory category)
        {
            var query=db.MarketplaceItems.AsNoTracking().Where(x=>x.Status==MarketplaceItemStatus.Published&&x.Category==category&&!blocked.Any(b=>b.BlockerId==x.AuthorId||b.BlockedId==x.AuthorId));
            if(!string.IsNullOrWhiteSpace(search)) query=query.Where(x=>x.Title.Contains(search)||x.Description.Contains(search));
            if(!string.IsNullOrWhiteSpace(technology)){var json=System.Text.Json.JsonSerializer.Serialize(new[]{technology});query=query.Where(x=>EF.Functions.JsonContains(x.TagsJson,json)||x.Description.Contains(technology));}
            if(!string.IsNullOrWhiteSpace(language)){var json=System.Text.Json.JsonSerializer.Serialize(new[]{language});query=query.Where(x=>EF.Functions.JsonContains(x.TagsJson,json));}
            var rows=sort=="recent"?query.OrderByDescending(x=>x.PublishedAt):query.OrderByDescending(x=>x.LikeCount).ThenByDescending(x=>x.DownloadCount).ThenByDescending(x=>x.PublishedAt);
            var raw=await rows.Take(limit).Select(x=>new{x.ID,x.Slug,x.Title,x.Description,x.Category,x.TagsJson,x.LikeCount,x.DownloadCount,Published=x.PublishedAt??x.UpdatedAt}).ToListAsync(ct);
            return raw.Select(x=>new DiscoverPackage(x.ID,x.Slug,x.Title,x.Description,x.Category,System.Text.Json.JsonSerializer.Deserialize<string[]>(x.TagsJson)??[],x.LikeCount,x.DownloadCount,x.Published)).ToList();
        }
        var templates=await Packages(MarketplaceCategory.ProjectTemplate); var agents=await Packages(MarketplaceCategory.AiAgent); var themes=await Packages(MarketplaceCategory.Theme);
        return new(developers,projects,snippets,templates,agents,themes,topics,$"Backend filters applied. Sort: {sort}. Popularity/trending use public follower, save, like and download counts; recent uses publish time. No hidden ML ranking.");
    }
}

public sealed class CreateSocialPostHandler(AppDbContext db, ICurrentUser user, INotificationService notifications, IActivityLogger activity) : IRequestHandler<CreateSocialPostCommand, SocialPostItem>
{
    public async Task<SocialPostItem> Handle(CreateSocialPostCommand request, CancellationToken ct)
    {
        if(request.Type is PostType.Achievement or PostType.Deployment)
            throw new ForbiddenException("Achievement and deployment posts are published only from verified server evidence.");
        if (request.ProjectId.HasValue)
        {
            var accessible = request.Type == PostType.ProjectShare && await db.Projects.AsNoTracking().AnyAsync(project =>
                project.ID == request.ProjectId && project.IsPublic && project.Members.Any(member => member.UserId == user.UserId), ct);
            if (!accessible) throw new ForbiddenException("Only a public project you belong to can be shared in the feed.");
        }
        var now = DateTime.UtcNow;
        var post = new SocialPost { ID = Guid.NewGuid(), AuthorId = user.UserId, Type = request.Type, Content = request.Content.Trim(), CodeLanguage = request.CodeLanguage?.Trim(), ImageUrl = request.ImageUrl?.Trim(), ProjectId = request.ProjectId, CreatedAt = now, UpdatedAt = now, CreatAt = now };
        db.SocialPosts.Add(post);
        await db.SaveChangesAsync(ct);
        var actor = await db.Users.AsNoTracking().Where(item => item.ID == user.UserId).Select(item => new { item.UserName, Name = item.FirstName + " " + item.LastName }).SingleAsync(ct);
        await SocialFeedSupport.NotifyMentions(db, notifications, user.UserId, actor.Name.Trim(), post.ID, post.Content, ct);
        await activity.LogAsync(new(user.UserId, request.ProjectId, "SocialPostCreated", nameof(SocialPost), post.ID, $"@{actor.UserName} created a {request.Type} post."), ct);
        return SocialFeedSupport.Item(await SocialFeedSupport.RequirePostRow(db, post.ID, user.UserId, ct), user.UserId);
    }
}

public sealed class UpdateSocialPostHandler(AppDbContext db, ICurrentUser user, INotificationService notifications) : IRequestHandler<UpdateSocialPostCommand, SocialPostItem>
{
    public async Task<SocialPostItem> Handle(UpdateSocialPostCommand request, CancellationToken ct)
    {
        var post = await db.SocialPosts.SingleOrDefaultAsync(item => item.ID == request.PostId, ct) ?? throw new NotFoundException("Post not found.");
        if (post.AuthorId != user.UserId) throw new ForbiddenException("You can edit only your own posts.");
        post.Content = request.Content.Trim(); post.CodeLanguage = request.CodeLanguage?.Trim(); post.ImageUrl = request.ImageUrl?.Trim();
        var now = DateTime.UtcNow; post.UpdatedAt = now; post.UpdateAt = now;
        await db.SaveChangesAsync(ct);
        var name = await db.Users.AsNoTracking().Where(item => item.ID == user.UserId).Select(item => item.FirstName + " " + item.LastName).SingleAsync(ct);
        await SocialFeedSupport.NotifyMentions(db, notifications, user.UserId, name.Trim(), post.ID, post.Content, ct);
        return SocialFeedSupport.Item(await SocialFeedSupport.RequirePostRow(db, post.ID, user.UserId, ct), user.UserId);
    }
}

public sealed class DeleteSocialPostHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity) : IRequestHandler<DeleteSocialPostCommand>
{
    public async Task Handle(DeleteSocialPostCommand request, CancellationToken ct)
    {
        var post = await db.SocialPosts.SingleOrDefaultAsync(item => item.ID == request.PostId, ct) ?? throw new NotFoundException("Post not found.");
        if (post.AuthorId != user.UserId) throw new ForbiddenException("You can delete only your own posts.");
        var now = DateTime.UtcNow; post.IsDeleted = true; post.DeletedAt = now; post.UpdateAt = now; post.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, post.ProjectId, "SocialPostDeleted", nameof(SocialPost), post.ID, "Deleted a social post."), ct);
    }
}

public sealed class TogglePostLikeHandler(AppDbContext db, ICurrentUser user, INotificationService notifications) : IRequestHandler<TogglePostLikeCommand, SocialToggleState>
{
    public async Task<SocialToggleState> Handle(TogglePostLikeCommand request, CancellationToken ct)
    {
        var post = await SocialFeedSupport.VisiblePosts(db, user.UserId).AsNoTracking().Where(item => item.ID == request.PostId).Select(item => new { item.ID, item.AuthorId }).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Post not found.");
        var reaction = await db.SocialPostReactions.SingleOrDefaultAsync(item => item.PostId == request.PostId && item.UserId == user.UserId, ct);
        var active = reaction is null;
        if (active) db.SocialPostReactions.Add(new() { ID = Guid.NewGuid(), PostId = request.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow }); else db.SocialPostReactions.Remove(reaction!);
        await db.SaveChangesAsync(ct);
        if (active && post.AuthorId != user.UserId)
        {
            var actor = await db.Users.AsNoTracking().Where(item => item.ID == user.UserId).Select(item => item.FirstName + " " + item.LastName).SingleAsync(ct);
            await notifications.CreateAsync(new(post.AuthorId, NotificationType.PostLike, "New post reaction", $"{actor.Trim()} liked your post.", post.ID, nameof(SocialPost)), ct);
        }
        return new(active, await db.SocialPostReactions.CountAsync(item => item.PostId == request.PostId, ct));
    }
}

public sealed class TogglePostSaveHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<TogglePostSaveCommand, SocialToggleState>
{
    public async Task<SocialToggleState> Handle(TogglePostSaveCommand request, CancellationToken ct)
    {
        if (!await SocialFeedSupport.VisiblePosts(db, user.UserId).AnyAsync(item => item.ID == request.PostId, ct)) throw new NotFoundException("Post not found.");
        var save = await db.SavedSocialPosts.SingleOrDefaultAsync(item => item.PostId == request.PostId && item.UserId == user.UserId, ct);
        var active = save is null;
        if (active) db.SavedSocialPosts.Add(new() { ID = Guid.NewGuid(), PostId = request.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow }); else db.SavedSocialPosts.Remove(save!);
        await db.SaveChangesAsync(ct);
        return new(active, await db.SavedSocialPosts.CountAsync(item => item.PostId == request.PostId, ct));
    }
}

public sealed class ShareSocialPostHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ShareSocialPostCommand, SocialToggleState>
{
    public async Task<SocialToggleState> Handle(ShareSocialPostCommand request, CancellationToken ct)
    {
        if (!await SocialFeedSupport.VisiblePosts(db, user.UserId).AnyAsync(item => item.ID == request.PostId, ct)) throw new NotFoundException("Post not found.");
        var exists = await db.SocialPostShares.AnyAsync(item => item.PostId == request.PostId && item.UserId == user.UserId, ct);
        if (!exists) { db.SocialPostShares.Add(new() { ID = Guid.NewGuid(), PostId = request.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync(ct); }
        return new(true, await db.SocialPostShares.CountAsync(item => item.PostId == request.PostId, ct));
    }
}

public sealed class GetSocialCommentsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetSocialCommentsQuery, SocialCommentPage>
{
    public async Task<SocialCommentPage> Handle(GetSocialCommentsQuery request, CancellationToken ct)
    {
        if (!await SocialFeedSupport.VisiblePosts(db, user.UserId).AnyAsync(item => item.ID == request.PostId, ct)) throw new NotFoundException("Post not found.");
        var limit = Math.Clamp(request.Limit, 1, 100); var cursor = SocialFeedSupport.Decode(request.Cursor);
        var query = db.SocialPostComments.AsNoTracking().Where(item => item.PostId == request.PostId);
        if (cursor is not null) query = query.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.ID.CompareTo(cursor.Id) < 0);
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.ID).Select(item => new SocialCommentItem(item.ID, item.PostId, item.ParentCommentId, item.Content,
            new(item.AuthorId, item.Author.PublicId, item.Author.UserName, (item.Author.FirstName + " " + item.Author.LastName).Trim(), item.Author.AvatarUrl), item.AuthorId == user.UserId, item.CreatedAt, item.UpdatedAt)).Take(limit + 1).ToListAsync(ct);
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1); var last = rows.LastOrDefault();
        return new(rows, hasMore && last is not null ? SocialFeedSupport.Encode(new(null, last.CreatedAt, last.Id)) : null);
    }
}

public sealed class AddSocialCommentHandler(AppDbContext db, ICurrentUser user, INotificationService notifications) : IRequestHandler<AddSocialCommentCommand, SocialCommentItem>
{
    public async Task<SocialCommentItem> Handle(AddSocialCommentCommand request, CancellationToken ct)
    {
        var post = await SocialFeedSupport.VisiblePosts(db, user.UserId).AsNoTracking().Where(item => item.ID == request.PostId).Select(item => new { item.ID, item.AuthorId }).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Post not found.");
        if (request.ParentCommentId.HasValue && !await db.SocialPostComments.AnyAsync(item => item.ID == request.ParentCommentId && item.PostId == request.PostId, ct)) throw new FluentValidation.ValidationException("The reply target does not belong to this post.");
        var now = DateTime.UtcNow; var comment = new SocialPostComment { ID = Guid.NewGuid(), PostId = request.PostId, AuthorId = user.UserId, ParentCommentId = request.ParentCommentId, Content = request.Content.Trim(), CreatedAt = now, UpdatedAt = now, CreatAt = now };
        db.SocialPostComments.Add(comment); await db.SaveChangesAsync(ct);
        var actor = await db.Users.AsNoTracking().Where(item => item.ID == user.UserId).Select(item => new { item.PublicId, item.UserName, item.FirstName, item.LastName, item.AvatarUrl }).SingleAsync(ct);
        var actorName = $"{actor.FirstName} {actor.LastName}".Trim();
        if (post.AuthorId != user.UserId) await notifications.CreateAsync(new(post.AuthorId, NotificationType.PostComment, "New post comment", $"{actorName} commented on your post.", post.ID, nameof(SocialPost)), ct);
        await SocialFeedSupport.NotifyMentions(db, notifications, user.UserId, actorName, post.ID, comment.Content, ct);
        return new(comment.ID, comment.PostId, comment.ParentCommentId, comment.Content, new(user.UserId, actor.PublicId, actor.UserName, actorName, actor.AvatarUrl), true, now, now);
    }
}

public sealed class DeleteSocialCommentHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<DeleteSocialCommentCommand>
{
    public async Task Handle(DeleteSocialCommentCommand request, CancellationToken ct)
    {
        var comment = await db.SocialPostComments.SingleOrDefaultAsync(item => item.ID == request.CommentId, ct) ?? throw new NotFoundException("Comment not found.");
        if (comment.AuthorId != user.UserId) throw new ForbiddenException("You can delete only your own comments.");
        comment.IsDeleted = true; comment.DeletedAt = comment.UpdateAt = comment.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }
}
