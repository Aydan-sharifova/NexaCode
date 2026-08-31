using Coding.Application.Abstractions;
using Coding.Application.Features.Search;
using Coding.Data;
using Coding.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Search;

public sealed class GlobalSearchHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResponse>
{
    public async Task<GlobalSearchResponse> Handle(GlobalSearchQuery request, CancellationToken ct)
    {
        var query = string.Join(' ', request.Query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var identityQuery = query.TrimStart('@');
        var pattern = $"%{identityQuery}%";
        var exactEmail = query.Contains('@') && !query.StartsWith('@');
        var offset = (request.Page - 1) * request.PageSize;
        var memberships = db.ProjectMembers.AsNoTracking().Where(x => x.UserId == currentUser.UserId);
        var groups = new List<SearchGroupDto>();

        if (request.Type is null or SearchResultType.Project)
        {
            var rows = await db.Projects.AsNoTracking()
                .Where(x => (memberships.Any(m => m.ProjectId == x.ID) || (x.IsPublic &&
                            !db.UserBlocks.Any(block => block.BlockerId == currentUser.UserId && block.BlockedId == x.OwnerId || block.BlockerId == x.OwnerId && block.BlockedId == currentUser.UserId)))
                    && (!request.ProjectId.HasValue || x.ID == request.ProjectId)
                    && (EF.Functions.ILike(x.Name, pattern) || (x.Description != null && EF.Functions.ILike(x.Description, pattern))))
                .OrderByDescending(x => EF.Functions.ILike(x.Name, query))
                .ThenByDescending(x => EF.Functions.ILike(x.Name, query + "%"))
                .ThenBy(x => x.Name)
                .Skip(offset).Take(request.PageSize + 1)
                .Select(x => new SearchResultDto(SearchResultType.Project, x.ID, x.Name, x.DefaultLanguage,
                    x.ID, x.Description ?? x.Name, memberships.Any(m => m.ProjectId == x.ID)
                        ? "/projects/" + x.ID + "/workspace"
                        : "/public/projects/" + x.ID,
                    EF.Functions.ILike(x.Name, query) ? 3 : EF.Functions.ILike(x.Name, query + "%") ? 2 : 1))
                .ToListAsync(ct);
            groups.Add(Group(SearchResultType.Project, rows, request.PageSize));
        }

        if (request.Type is null or SearchResultType.File)
        {
            var rows = await db.WorkspaceNodes.AsNoTracking()
                .Where(x => memberships.Any(m => m.ProjectId == x.ProjectId)
                    && (!request.ProjectId.HasValue || x.ProjectId == request.ProjectId)
                    && x.NodeType == WorkspaceNodeType.File && EF.Functions.ILike(x.Name, pattern))
                .OrderByDescending(x => EF.Functions.ILike(x.Name, query))
                .ThenByDescending(x => EF.Functions.ILike(x.Name, query + "%"))
                .ThenBy(x => x.Name)
                .Skip(offset).Take(request.PageSize + 1)
                .Select(x => new SearchResultDto(SearchResultType.File, x.ID, x.Name, x.Project.Name,
                    x.ProjectId, x.Name, $"/projects/{x.ProjectId}/workspace?file={x.ID}",
                    EF.Functions.ILike(x.Name, query) ? 3 : EF.Functions.ILike(x.Name, query + "%") ? 2 : 1))
                .ToListAsync(ct);
            groups.Add(Group(SearchResultType.File, rows, request.PageSize));
        }

        if (request.Type is null or SearchResultType.Task)
        {
            var rows = await db.ProjectTasks.AsNoTracking()
                .Where(x => memberships.Any(m => m.ProjectId == x.ProjectId)
                    && (!request.ProjectId.HasValue || x.ProjectId == request.ProjectId)
                    && (EF.Functions.ILike(x.Title, pattern) || (x.Description != null && EF.Functions.ILike(x.Description, pattern))))
                .OrderByDescending(x => EF.Functions.ILike(x.Title, query))
                .ThenByDescending(x => EF.Functions.ILike(x.Title, query + "%"))
                .ThenBy(x => x.Title)
                .Skip(offset).Take(request.PageSize + 1)
                .Select(x => new SearchResultDto(SearchResultType.Task, x.ID, x.Title, x.Project.Name,
                    x.ProjectId, x.Description ?? x.Title, $"/projects/{x.ProjectId}/board?task={x.ID}",
                    EF.Functions.ILike(x.Title, query) ? 3 : EF.Functions.ILike(x.Title, query + "%") ? 2 : 1))
                .ToListAsync(ct);
            groups.Add(Group(SearchResultType.Task, rows, request.PageSize));
        }

        if (request.Type is null or SearchResultType.User)
        {
            var rows = await db.Users.AsNoTracking()
                .Where(x => !x.IsDeleted && !x.IsSuspended
                    && (x.ID == currentUser.UserId || x.DeveloperProfile == null || x.DeveloperProfile.IsProfilePublic)
                    && !db.UserBlocks.Any(block => block.BlockerId == currentUser.UserId && block.BlockedId == x.ID || block.BlockerId == x.ID && block.BlockedId == currentUser.UserId)
                    && (exactEmail
                        ? x.Email.ToLower() == query
                        : EF.Functions.ILike(x.PublicId, pattern) || EF.Functions.ILike(x.UserName, pattern) || EF.Functions.ILike(x.FirstName + " " + x.LastName, pattern)))
                .OrderByDescending(x => EF.Functions.ILike(x.PublicId, identityQuery))
                .ThenByDescending(x => EF.Functions.ILike(x.UserName, identityQuery))
                .ThenBy(x => x.UserName)
                .Skip(offset).Take(request.PageSize + 1)
                .Select(x => new SearchResultDto(SearchResultType.User, x.ID, x.FirstName + " " + x.LastName,
                    "@" + x.UserName,
                    null,
                    x.UserName, "/users/" + x.PublicId,
                    EF.Functions.ILike(x.PublicId, identityQuery) || EF.Functions.ILike(x.UserName, identityQuery) ? 3 :
                    EF.Functions.ILike(x.PublicId, identityQuery + "%") || EF.Functions.ILike(x.UserName, identityQuery + "%") ? 2 : 1))
                .ToListAsync(ct);
            groups.Add(Group(SearchResultType.User, rows, request.PageSize));
        }

        return new GlobalSearchResponse(query, request.Page, request.PageSize, groups);
    }

    private static SearchGroupDto Group(SearchResultType type, List<SearchResultDto> rows, int size) =>
        new(type, rows.Take(size).ToList(), rows.Count > size);
}
