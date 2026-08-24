using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.SocialFeed;

public enum FeedTab { ForYou, Following, Trending }

public sealed record FeedAuthor(Guid Id, string PublicId, string UserName, string DisplayName, string? AvatarUrl);
public sealed record FeedProject(Guid Id, string Name);
public sealed record SocialPostItem(
    Guid Id, PostType Type, string Content, string? CodeLanguage, string? ImageUrl,
    FeedAuthor Author, FeedProject? Project, int LikeCount, int CommentCount, int SaveCount,
    int ShareCount, bool IsLiked, bool IsSaved, bool IsOwner, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SocialPostPage(IReadOnlyList<SocialPostItem> Items, string? NextCursor);
public sealed record SocialCommentItem(Guid Id, Guid PostId, Guid? ParentCommentId, string Content, FeedAuthor Author, bool IsOwner, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SocialCommentPage(IReadOnlyList<SocialCommentItem> Items, string? NextCursor);
public sealed record SocialToggleState(bool Active, int Count);
public sealed record DiscoverDeveloper(Guid Id,string PublicId,string UserName,string DisplayName,string? AvatarUrl,int Followers,int Posts);
public sealed record DiscoverProject(Guid Id,string Name,string? Description,string OwnerPublicId,int Saves);
public sealed record DiscoverTopic(string Name,int Posts);
public sealed record DiscoverSnippet(Guid Id,string Content,string Language,FeedAuthor Author,int Likes,int Saves,DateTime CreatedAt);
public sealed record DiscoverPackage(Guid Id,string Slug,string Title,string Description,MarketplaceCategory Category,IReadOnlyList<string> Tags,int Likes,int Downloads,DateTime PublishedAt);
public sealed record SocialDiscover(IReadOnlyList<DiscoverDeveloper> Developers,IReadOnlyList<DiscoverProject> Projects,IReadOnlyList<DiscoverSnippet> Snippets,IReadOnlyList<DiscoverPackage> Templates,IReadOnlyList<DiscoverPackage> Agents,IReadOnlyList<DiscoverPackage> Themes,IReadOnlyList<DiscoverTopic> Topics,string RankingExplanation);

public sealed record GetSocialFeedQuery(FeedTab Tab, string? Cursor, int Limit = 20) : IRequest<SocialPostPage>;
public sealed record GetSavedPostsQuery(string? Cursor, int Limit = 20) : IRequest<SocialPostPage>;
public sealed record GetSocialDiscoverQuery(string? Search=null,string? Technology=null,string? Language=null,string Sort="Trending",int Limit = 8) : IRequest<SocialDiscover>;
public sealed record CreateSocialPostCommand(PostType Type, string Content, string? CodeLanguage, string? ImageUrl, Guid? ProjectId) : IRequest<SocialPostItem>;
public sealed record UpdateSocialPostCommand(Guid PostId, string Content, string? CodeLanguage, string? ImageUrl) : IRequest<SocialPostItem>;
public sealed record DeleteSocialPostCommand(Guid PostId) : IRequest;
public sealed record TogglePostLikeCommand(Guid PostId) : IRequest<SocialToggleState>;
public sealed record TogglePostSaveCommand(Guid PostId) : IRequest<SocialToggleState>;
public sealed record ShareSocialPostCommand(Guid PostId) : IRequest<SocialToggleState>;
public sealed record GetSocialCommentsQuery(Guid PostId, string? Cursor, int Limit = 30) : IRequest<SocialCommentPage>;
public sealed record AddSocialCommentCommand(Guid PostId, Guid? ParentCommentId, string Content) : IRequest<SocialCommentItem>;
public sealed record DeleteSocialCommentCommand(Guid CommentId) : IRequest;

public sealed class CreateSocialPostValidator : AbstractValidator<CreateSocialPostCommand>
{
    public CreateSocialPostValidator()
    {
        RuleFor(item => item.Content).NotEmpty().MaximumLength(10_000);
        RuleFor(item => item.CodeLanguage).MaximumLength(50);
        RuleFor(item => item.ImageUrl).MaximumLength(500).Must(BeHttpUrl).When(item => !string.IsNullOrWhiteSpace(item.ImageUrl));
        RuleFor(item => item.ProjectId).NotNull().When(item => item.Type == PostType.ProjectShare);
        RuleFor(item => item.CodeLanguage).NotEmpty().When(item => item.Type == PostType.Code);
        RuleFor(item => item.ImageUrl).NotEmpty().When(item => item.Type == PostType.Image);
        RuleFor(item => item.Type).Must(type => type is not PostType.Achievement and not PostType.Deployment)
            .WithMessage("Achievement and deployment posts are published only from verified server evidence.");
    }

    private static bool BeHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed class UpdateSocialPostValidator : AbstractValidator<UpdateSocialPostCommand>
{
    public UpdateSocialPostValidator()
    {
        RuleFor(item => item.PostId).NotEmpty();
        RuleFor(item => item.Content).NotEmpty().MaximumLength(10_000);
        RuleFor(item => item.CodeLanguage).MaximumLength(50);
        RuleFor(item => item.ImageUrl).MaximumLength(500).Must(value => string.IsNullOrWhiteSpace(value) || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
    }
}

public sealed class AddSocialCommentValidator : AbstractValidator<AddSocialCommentCommand>
{
    public AddSocialCommentValidator()
    {
        RuleFor(item => item.PostId).NotEmpty();
        RuleFor(item => item.Content).NotEmpty().MaximumLength(2_000);
    }
}
