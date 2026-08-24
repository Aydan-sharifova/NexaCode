using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Users;

public sealed record UserIdentity(Guid Id, string PublicId, string DisplayName, string UserName, string? AvatarUrl);
public sealed record UserSearchResultDto(string PublicId, string DisplayName, string UserName, string? AvatarUrl, string? Bio, int PublicProjectCount);
public sealed record UserSearchPage(IReadOnlyList<UserSearchResultDto> Items, int Page, int PageSize, bool HasMore);
public sealed record PublicProjectDto(Guid Id, string Name, string? Description, string DefaultLanguage, DateTime UpdatedAt);
public sealed record PublicProjectPage(IReadOnlyList<PublicProjectDto> Items, int Page, int PageSize, bool HasMore);
public sealed record PublicProjectDetailsDto(Guid Id, string Name, string? Description, string DefaultLanguage, string OwnerPublicId, string OwnerDisplayName, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record PublicProjectNodeDto(Guid Id, Guid? ParentId, string Name, string NodeType, string Path, bool HasChildren);
public sealed record PublicProjectFileDto(Guid Id, string Path, string Content, int VersionNumber, DateTime UpdatedAt);
public sealed record PublicUserProfileDto(
    Guid Id,
    string PublicId,
    string DisplayName,
    string UserName,
    string? AvatarUrl,
    string? CoverImageUrl,
    string? Bio,
    string? Headline,
    string? Location,
    string? WebsiteUrl,
    string? GitHubUrl,
    string? LinkedInUrl,
    string? PortfolioUrl,
    string? PrimaryRole,
    string? ExperienceLevel,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> LearningTopics,
    DateTime JoinedAt,
    int PublicProjectCount,
    int? FollowerCount,
    int? FollowingCount,
    bool IsFollowing,
    bool IsBlockedByMe,
    bool IsOwnProfile,
    bool IsProfilePublic,
    bool IsActivityPublic,
    bool AreFollowersPublic);
public sealed record PortfolioPostDto(Guid Id,string Type,string Content,string? CodeLanguage,string? ImageUrl,Guid? ProjectId,string? ProjectName,int Likes,int Comments,int Saves,int Shares,DateTime CreatedAt);
public sealed record PortfolioActivityDto(string Type,string Title,string Description,DateTime OccurredAt,Guid? EvidenceId);
public sealed record PortfolioPersonDto(string PublicId,string UserName,string DisplayName,string? AvatarUrl);
public sealed record ContributionSummaryDto(int Commits,int MergedPullRequests,int AcceptedReviews,int PublishedProjects,int UsefulSnippets,int Deployments,int CommunityPosts,int VerifiedAchievements);
public sealed record DeveloperPortfolioDto(bool ActivityVisible,IReadOnlyList<PortfolioPostDto> Posts,IReadOnlyList<PortfolioPostDto> Snippets,
    IReadOnlyList<PortfolioActivityDto> Activity,ContributionSummaryDto? Contributions,IReadOnlyList<PortfolioPersonDto> Followers,IReadOnlyList<PortfolioPersonDto> Following);

public sealed record UpdateDeveloperProfileCommand(
    string DisplayName,
    string? Bio,
    string? Headline,
    string? Location,
    string? WebsiteUrl,
    string? GitHubUrl,
    string? LinkedInUrl,
    string? PortfolioUrl,
    string? PrimaryRole,
    string? ExperienceLevel,
    IReadOnlyList<string>? Skills,
    IReadOnlyList<string>? LearningTopics,
    bool IsProfilePublic,
    bool IsActivityPublic,
    bool AreFollowersPublic) : IRequest<PublicUserProfileDto>;

public sealed record FollowStateDto(bool IsFollowing, int? FollowerCount);
public sealed record FollowUserCommand(string PublicId) : IRequest<FollowStateDto>;
public sealed record UnfollowUserCommand(string PublicId) : IRequest<FollowStateDto>;
public sealed record BlockStateDto(bool IsBlocked);
public sealed record BlockedUserDto(string PublicId, string DisplayName, string UserName, string? AvatarUrl, DateTime BlockedAt);
public sealed record BlockedUserPage(IReadOnlyList<BlockedUserDto> Items, string? NextCursor);
public sealed record BlockUserCommand(string PublicId) : IRequest<BlockStateDto>;
public sealed record UnblockUserCommand(string PublicId) : IRequest<BlockStateDto>;
public sealed record GetBlockedUsersQuery(string? Cursor, int Limit = 30) : IRequest<BlockedUserPage>;

public sealed record SearchUsersQuery(string Query, int Page = 1, int PageSize = 20) : IRequest<UserSearchPage>;
public sealed record GetPublicUserProfileQuery(string PublicId) : IRequest<PublicUserProfileDto>;
public sealed record GetDeveloperPortfolioQuery(string PublicId) : IRequest<DeveloperPortfolioDto>;
public sealed record GetPublicUserProjectsQuery(string PublicId, int Page = 1, int PageSize = 12) : IRequest<PublicProjectPage>;
public sealed record GetPublicProjectDetailsQuery(Guid ProjectId) : IRequest<PublicProjectDetailsDto>;
public sealed record GetPublicProjectTreeQuery(Guid ProjectId) : IRequest<IReadOnlyList<PublicProjectNodeDto>>;
public sealed record GetPublicProjectFileQuery(Guid ProjectId, Guid NodeId) : IRequest<PublicProjectFileDto>;

public sealed class UpdateDeveloperProfileValidator : AbstractValidator<UpdateDeveloperProfileCommand>
{
    public UpdateDeveloperProfileValidator()
    {
        RuleFor(item => item.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(item => item.Bio).MaximumLength(1000);
        RuleFor(item => item.Headline).MaximumLength(160);
        RuleFor(item => item.Location).MaximumLength(120);
        RuleFor(item => item.PrimaryRole).MaximumLength(100);
        RuleFor(item => item.ExperienceLevel).MaximumLength(40);
        RuleFor(item => item.Skills).Must(items => IsValidTags(items, 30, 40)).WithMessage("Skills must contain at most 30 unique values of 40 characters or fewer.");
        RuleFor(item => item.LearningTopics).Must(items => IsValidTags(items, 20, 80)).WithMessage("Learning topics must contain at most 20 unique values of 80 characters or fewer.");
        RuleFor(item => item.WebsiteUrl).Must(BeWebUrl).WithMessage("Profile links must be valid HTTP or HTTPS URLs.");
        RuleFor(item => item.GitHubUrl).Must(BeWebUrl).WithMessage("Profile links must be valid HTTP or HTTPS URLs.");
        RuleFor(item => item.LinkedInUrl).Must(BeWebUrl).WithMessage("Profile links must be valid HTTP or HTTPS URLs.");
        RuleFor(item => item.PortfolioUrl).Must(BeWebUrl).WithMessage("Profile links must be valid HTTP or HTTPS URLs.");
    }

    private static bool BeWebUrl(string? value) => string.IsNullOrWhiteSpace(value) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static bool IsValidTags(IReadOnlyList<string>? items, int maximum, int maximumLength) => items is null ||
        items.Count <= maximum && items.All(item => !string.IsNullOrWhiteSpace(item) && item.Trim().Length <= maximumLength) &&
        items.Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count;
}

public interface IUserLookupService
{
    Task<UserIdentity?> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken);
    Task<UserIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserIdentity?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);
    Task<UserSearchPage> SearchAsync(Guid viewerId, string query, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IPublicUserIdGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
