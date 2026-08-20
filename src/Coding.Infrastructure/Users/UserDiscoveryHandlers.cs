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
        var identifier = request.PublicId.Trim().TrimStart('@');
        var publicId = identifier.ToUpperInvariant();
        var hasUserId = Guid.TryParse(identifier, out var userId);
        return await db.Users.AsNoTracking().Where(user => !user.IsDeleted && !user.IsSuspended &&
                (user.PublicId == publicId || EF.Functions.ILike(user.UserName, identifier) || (hasUserId && user.ID == userId)))
            .Select(user => new PublicUserProfileDto(user.PublicId, (user.FirstName + " " + user.LastName).Trim(), user.UserName,
                user.AvatarUrl, user.Bio, user.CreatedAt, user.OwnedProjects.Count(project => project.IsPublic)))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
    }
}

public sealed class GetPublicUserProjectsHandler(AppDbContext db) : IRequestHandler<GetPublicUserProjectsQuery, PublicProjectPage>
{
    public async Task<PublicProjectPage> Handle(GetPublicUserProjectsQuery request, CancellationToken ct)
    {
        var identifier = request.PublicId.Trim().TrimStart('@');
        var publicId = identifier.ToUpperInvariant();
        var hasUserId = Guid.TryParse(identifier, out var userId);
        var ownerId = await db.Users.AsNoTracking()
            .Where(user => !user.IsDeleted && !user.IsSuspended &&
                (user.PublicId == publicId || EF.Functions.ILike(user.UserName, identifier) || (hasUserId && user.ID == userId)))
            .Select(user => (Guid?)user.ID).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");
        var rows = await db.Projects.AsNoTracking()
            .Where(project => project.IsPublic && project.OwnerId == ownerId)
            .OrderByDescending(project => project.UpdateAt ?? project.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize + 1)
            .Select(project => new PublicProjectDto(project.ID, project.Name, project.Description, project.DefaultLanguage,
                project.UpdateAt ?? project.CreatedAt)).ToListAsync(ct);
        return new(rows.Take(request.PageSize).ToList(), request.Page, request.PageSize, rows.Count > request.PageSize);
    }
}

public sealed class GetPublicProjectDetailsHandler(AppDbContext db) : IRequestHandler<GetPublicProjectDetailsQuery, PublicProjectDetailsDto>
{
    public async Task<PublicProjectDetailsDto> Handle(GetPublicProjectDetailsQuery request, CancellationToken ct) =>
        await db.Projects.AsNoTracking().Where(project => project.ID == request.ProjectId && project.IsPublic)
            .Select(project => new PublicProjectDetailsDto(project.ID, project.Name, project.Description, project.DefaultLanguage,
                project.Owner.PublicId, (project.Owner.FirstName + " " + project.Owner.LastName).Trim(), project.CreatedAt,
                project.UpdateAt ?? project.CreatedAt))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Public project not found.");
}

public sealed class GetPublicProjectTreeHandler(AppDbContext db) : IRequestHandler<GetPublicProjectTreeQuery, IReadOnlyList<PublicProjectNodeDto>>
{
    public async Task<IReadOnlyList<PublicProjectNodeDto>> Handle(GetPublicProjectTreeQuery request, CancellationToken ct)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(project => project.ID == request.ProjectId && project.IsPublic, ct))
            throw new NotFoundException("Public project not found.");
        return await db.WorkspaceNodes.AsNoTracking().Where(node => node.ProjectId == request.ProjectId)
            .OrderBy(node => node.NodeType).ThenBy(node => node.Name)
            .Select(node => new PublicProjectNodeDto(node.ID, node.ParentId, node.Name,
                node.NodeType == Coding.Enums.WorkspaceNodeType.Folder ? "Folder" : "File", node.Name,
                db.WorkspaceNodes.Any(child => child.ParentId == node.ID)))
            .ToListAsync(ct);
    }
}

public sealed class GetPublicProjectFileHandler(AppDbContext db) : IRequestHandler<GetPublicProjectFileQuery, PublicProjectFileDto>
{
    public async Task<PublicProjectFileDto> Handle(GetPublicProjectFileQuery request, CancellationToken ct) =>
        await db.FileContents.AsNoTracking()
            .Where(file => file.NodeId == request.NodeId && file.Node.ProjectId == request.ProjectId && file.Node.Project.IsPublic)
            .Select(file => new PublicProjectFileDto(file.NodeId, file.Node.Name, file.Content, file.VersionNumber, file.UpdatedAt))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Public project file not found.");
}
