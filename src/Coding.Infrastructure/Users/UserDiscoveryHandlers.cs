using Coding.Application.Features.Users;
using Coding.Data;
using Coding.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Users;

public sealed class SearchUsersHandler(IUserLookupService users) : IRequestHandler<SearchUsersQuery, UserSearchPage>
{
    public Task<UserSearchPage> Handle(SearchUsersQuery request, CancellationToken ct) =>
        users.SearchAsync(request.Query, request.Page, request.PageSize, ct);
}

public sealed class GetPublicUserProfileHandler(AppDbContext db) : IRequestHandler<GetPublicUserProfileQuery, PublicUserProfileDto>
{
    public async Task<PublicUserProfileDto> Handle(GetPublicUserProfileQuery request, CancellationToken ct)
    {
        var publicId = request.PublicId.Trim().TrimStart('@').ToUpperInvariant();
        return await db.Users.AsNoTracking().Where(user => !user.IsDeleted && !user.IsSuspended && user.PublicId == publicId)
            .Select(user => new PublicUserProfileDto(user.PublicId, (user.FirstName + " " + user.LastName).Trim(), user.UserName,
                user.AvatarUrl, user.Bio, user.CreatedAt, user.OwnedProjects.Count(project => project.IsPublic)))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
    }
}

public sealed class GetPublicUserProjectsHandler(AppDbContext db) : IRequestHandler<GetPublicUserProjectsQuery, PublicProjectPage>
{
    public async Task<PublicProjectPage> Handle(GetPublicUserProjectsQuery request, CancellationToken ct)
    {
        var publicId = request.PublicId.Trim().TrimStart('@').ToUpperInvariant();
        if (!await db.Users.AsNoTracking().AnyAsync(user => !user.IsDeleted && user.PublicId == publicId, ct))
            throw new NotFoundException("User not found.");
        var rows = await db.Projects.AsNoTracking()
            .Where(project => project.IsPublic && project.Owner.PublicId == publicId)
            .OrderByDescending(project => project.UpdateAt ?? project.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize + 1)
            .Select(project => new PublicProjectDto(project.ID, project.Name, project.Description, project.DefaultLanguage,
                project.UpdateAt ?? project.CreatedAt)).ToListAsync(ct);
        return new(rows.Take(request.PageSize).ToList(), request.Page, request.PageSize, rows.Count > request.PageSize);
    }
}
