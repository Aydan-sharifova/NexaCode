using MediatR;

namespace Coding.Application.Features.Administration;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record AdminUserListItem(Guid Id, string DisplayName, string UserName, string Email, bool IsSuspended, IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime LastSeen);
public sealed record AdminUserDetails(Guid Id, string FirstName, string LastName, string UserName, string Email, string? Bio, string? AvatarUrl, bool IsSuspended, string? SuspensionReason, IReadOnlyList<string> Roles, int ProjectCount, DateTime CreatedAt, DateTime LastSeen);
public sealed record AdminProjectItem(Guid Id, string Name, string OwnerName, bool IsPublic, int MemberCount, int TaskCount, DateTime CreatedAt);
public sealed record PlatformStatistics(int TotalUsers, int ActiveUsers30Days, int SuspendedUsers, int TotalProjects, int Projects30Days, int Activity30Days);
public sealed record ProgrammingLanguageItem(Guid Id, string Name, string Slug, int SortOrder, bool IsActive);

public sealed record GetAdminUsersQuery(string? Search, bool? Suspended, string? Role, int Page = 1, int PageSize = 25) : IRequest<PageResult<AdminUserListItem>>;
public sealed record GetAdminUserDetailsQuery(Guid UserId) : IRequest<AdminUserDetails>;
public sealed record SetUserSuspensionCommand(Guid UserId, bool Suspended, string? Reason) : IRequest;
public sealed record SetSystemRoleCommand(Guid UserId, string Role, bool Enabled) : IRequest;
public sealed record UpdateAdminUserCommand(Guid UserId, string FirstName, string LastName, string UserName, string Email, string? Bio) : IRequest<AdminUserDetails>;
public sealed record DeleteAdminUserCommand(Guid UserId, string Reason) : IRequest;
public sealed record GetAdminProjectsQuery(string? Search, int Page = 1, int PageSize = 25) : IRequest<PageResult<AdminProjectItem>>;
public sealed record DeleteAbusiveProjectCommand(Guid ProjectId, string Reason) : IRequest;
public sealed record GetPlatformStatisticsQuery : IRequest<PlatformStatistics>;
public sealed record ListProgrammingLanguagesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<ProgrammingLanguageItem>>;
public sealed record CreateProgrammingLanguageCommand(string Name, string? Slug, int SortOrder) : IRequest<ProgrammingLanguageItem>;
public sealed record UpdateProgrammingLanguageCommand(Guid Id, string Name, string? Slug, int SortOrder, bool IsActive) : IRequest<ProgrammingLanguageItem>;
public sealed record DeleteProgrammingLanguageCommand(Guid Id) : IRequest;
