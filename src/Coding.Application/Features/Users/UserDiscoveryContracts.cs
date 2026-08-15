using MediatR;

namespace Coding.Application.Features.Users;

public sealed record UserIdentity(Guid Id, string PublicId, string DisplayName, string UserName, string? AvatarUrl);
public sealed record UserSearchResultDto(string PublicId, string DisplayName, string UserName, string? AvatarUrl, string? Bio, int PublicProjectCount);
public sealed record UserSearchPage(IReadOnlyList<UserSearchResultDto> Items, int Page, int PageSize, bool HasMore);
public sealed record PublicProjectDto(Guid Id, string Name, string? Description, string DefaultLanguage, DateTime UpdatedAt);
public sealed record PublicProjectPage(IReadOnlyList<PublicProjectDto> Items, int Page, int PageSize, bool HasMore);
public sealed record PublicUserProfileDto(string PublicId, string DisplayName, string UserName, string? AvatarUrl, string? Bio, DateTime JoinedAt, int PublicProjectCount);

public sealed record SearchUsersQuery(string Query, int Page = 1, int PageSize = 20) : IRequest<UserSearchPage>;
public sealed record GetPublicUserProfileQuery(string PublicId) : IRequest<PublicUserProfileDto>;
public sealed record GetPublicUserProjectsQuery(string PublicId, int Page = 1, int PageSize = 12) : IRequest<PublicProjectPage>;

public interface IUserLookupService
{
    Task<UserIdentity?> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken);
    Task<UserIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserIdentity?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);
    Task<UserSearchPage> SearchAsync(string query, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IPublicUserIdGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
