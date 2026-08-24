using Coding.Application.Features.Users;
using Coding.Data;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Users;

public sealed class UserLookupService(AppDbContext db) : IUserLookupService
{
    public Task<UserIdentity?> FindByPublicIdAsync(string publicId, CancellationToken ct) =>
        IdentityQuery().SingleOrDefaultAsync(user => user.PublicId == NormalizePublicId(publicId), ct);

    public Task<UserIdentity?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.Users.AsNoTracking().Where(user => !user.IsDeleted && user.Email.ToLower() == normalized)
            .Select(user => new UserIdentity(user.ID, user.PublicId, (user.FirstName + " " + user.LastName).Trim(), user.UserName, user.AvatarUrl))
            .SingleOrDefaultAsync(ct);
    }

    public Task<UserIdentity?> FindByIdentifierAsync(string identifier, CancellationToken ct) =>
        identifier.Contains('@') ? FindByEmailAsync(identifier, ct) : FindByPublicIdAsync(identifier, ct);

    public async Task<UserSearchPage> SearchAsync(Guid viewerId, string query, int page, int pageSize, CancellationToken ct)
    {
        var trimmed = query.Trim();
        var normalized = trimmed.ToLowerInvariant();
        var exactEmail = trimmed.Contains('@');
        var offset = (page - 1) * pageSize;
        var users = db.Users.AsNoTracking().Where(user => !user.IsDeleted && !user.IsSuspended &&
            !db.UserBlocks.Any(block => block.BlockerId == viewerId && block.BlockedId == user.ID || block.BlockerId == user.ID && block.BlockedId == viewerId));
        users = exactEmail
            ? users.Where(user => user.Email.ToLower() == normalized)
            : users.Where(user => EF.Functions.ILike(user.PublicId, $"%{trimmed}%") ||
                                  EF.Functions.ILike(user.UserName, $"%{trimmed}%") ||
                                  EF.Functions.ILike(user.FirstName + " " + user.LastName, $"%{trimmed}%"));
        var rows = await users.OrderByDescending(user => user.PublicId == trimmed.ToUpper())
            .ThenBy(user => user.UserName).Skip(offset).Take(pageSize + 1)
            .Select(user => new UserSearchResultDto(user.PublicId, (user.FirstName + " " + user.LastName).Trim(), user.UserName,
                user.AvatarUrl, user.Bio, user.OwnedProjects.Count(project => project.IsPublic)))
            .ToListAsync(ct);
        return new(rows.Take(pageSize).ToList(), page, pageSize, rows.Count > pageSize);
    }

    private IQueryable<UserIdentity> IdentityQuery() => db.Users.AsNoTracking()
        .Where(user => !user.IsDeleted && !user.IsSuspended)
        .Select(user => new UserIdentity(user.ID, user.PublicId, (user.FirstName + " " + user.LastName).Trim(), user.UserName, user.AvatarUrl));

    private static string NormalizePublicId(string value) => value.Trim().TrimStart('@').ToUpperInvariant();
}
