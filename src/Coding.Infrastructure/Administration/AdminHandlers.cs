using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Administration;
using Coding.Data;
using Coding.Infrastructure.Authentication;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Administration;

internal static class AdminAccess
{
    public static Task<bool> IsSuperAdmin(AppDbContext db, Guid userId, CancellationToken ct) =>
        db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.Name == SystemRoles.SuperAdmin, ct);
}
public sealed class GetAdminUsersHandler(AppDbContext db) : IRequestHandler<GetAdminUsersQuery, PageResult<AdminUserListItem>>
{
    public async Task<PageResult<AdminUserListItem>> Handle(GetAdminUsersQuery r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 100); var query = db.Users.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(r.Search)) { var p = $"%{r.Search.Trim()}%"; query = query.Where(x => EF.Functions.ILike(x.Email, p) || EF.Functions.ILike(x.UserName, p) || EF.Functions.ILike(x.FirstName + " " + x.LastName, p)); }
        if (r.Suspended.HasValue) query = query.Where(x => x.IsSuspended == r.Suspended);
        if (!string.IsNullOrWhiteSpace(r.Role)) query = query.Where(x => x.UserRoles.Any(ur => ur.Role.Name == r.Role));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(x => new AdminUserListItem(x.ID, x.FirstName + " " + x.LastName, x.UserName, x.Email, x.IsSuspended, x.UserRoles.Select(ur => ur.Role.Name).ToList(), x.CreatedAt, x.LastSeen)).ToListAsync(ct);
        return new(items, total, page, size);
    }
}
public sealed class GetAdminUserDetailsHandler(AppDbContext db) : IRequestHandler<GetAdminUserDetailsQuery, AdminUserDetails>
{
    public async Task<AdminUserDetails> Handle(GetAdminUserDetailsQuery r, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => x.ID == r.UserId && !x.IsDeleted)
            .Select(x => new AdminUserDetails(x.ID, x.FirstName, x.LastName, x.UserName, x.Email, x.Bio, x.AvatarUrl, x.IsSuspended, x.SuspensionReason, x.UserRoles.Select(ur => ur.Role.Name).ToList(), x.ProjectMembers.Count, x.CreatedAt, x.LastSeen))
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("User was not found.");
}
public sealed class SetUserSuspensionHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<SetUserSuspensionCommand>
{
    public async Task Handle(SetUserSuspensionCommand r, CancellationToken ct)
    {
        if (r.UserId == current.UserId) throw new InvalidOperationException("You cannot suspend your own account.");
        var target = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.ID == r.UserId, ct) ?? throw new KeyNotFoundException("User was not found.");
        if (target.UserRoles.Any(x => x.Role.Name == SystemRoles.SuperAdmin) && !await AdminAccess.IsSuperAdmin(db, current.UserId, ct)) throw new UnauthorizedAccessException("Only a SuperAdmin can manage another SuperAdmin.");
        target.IsSuspended = r.Suspended; target.SuspendedAt = r.Suspended ? DateTime.UtcNow : null; target.SuspensionReason = r.Suspended ? r.Reason?.Trim() : null; target.UpdatedAt = DateTime.UtcNow;
        if (r.Suspended) await db.RefreshTokens.Where(x => x.UserId == target.ID && !x.IsRevoked).ExecuteUpdateAsync(x => x.SetProperty(t => t.IsRevoked, true), ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, null, r.Suspended ? "AdminUserSuspended" : "AdminUserActivated", nameof(User), target.ID, r.Suspended ? "Administrator suspended a user." : "Administrator activated a user.", new Dictionary<string, object?> { ["reason"] = r.Reason }), ct);
    }
}
public sealed class SetSystemRoleHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<SetSystemRoleCommand>
{
    public async Task Handle(SetSystemRoleCommand r, CancellationToken ct)
    {
        if (!await AdminAccess.IsSuperAdmin(db, current.UserId, ct)) throw new UnauthorizedAccessException("Only SuperAdmin may manage system roles.");
        if (!SystemRoles.All.Contains(r.Role)) throw new InvalidOperationException("This role is not a permitted system role.");
        if (!r.Enabled && r.Role == SystemRoles.SuperAdmin)
        {
            if (r.UserId == current.UserId) throw new InvalidOperationException("You cannot remove your own SuperAdmin role.");
            var count = await db.UserRoles.CountAsync(x => x.Role.Name == SystemRoles.SuperAdmin && !x.User.IsDeleted, ct);
            if (count <= 1) throw new InvalidOperationException("The last SuperAdmin role cannot be removed.");
        }
        var role = await db.Roles.SingleAsync(x => x.Name == r.Role, ct); var assignment = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == r.UserId && x.RoleId == role.ID, ct);
        if (r.Enabled && assignment is null) db.UserRoles.Add(new UserRole { ID = Guid.NewGuid(), UserId = r.UserId, RoleId = role.ID });
        if (!r.Enabled && assignment is not null) db.UserRoles.Remove(assignment);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, null, r.Enabled ? "SystemRoleGranted" : "SystemRoleRevoked", nameof(User), r.UserId, $"{r.Role} role {(r.Enabled ? "granted" : "revoked")}."), ct);
    }
}
public sealed class UpdateAdminUserHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<UpdateAdminUserCommand, AdminUserDetails>
{
    public async Task<AdminUserDetails> Handle(UpdateAdminUserCommand r, CancellationToken ct)
    {
        if (!await AdminAccess.IsSuperAdmin(db, current.UserId, ct)) throw new UnauthorizedAccessException("Only SuperAdmin may edit users.");
        var email = r.Email.Trim().ToLowerInvariant(); var userName = r.UserName.Trim();
        if (await db.Users.AnyAsync(x => x.ID != r.UserId && (x.Email.ToLower() == email || x.UserName.ToLower() == userName.ToLower()), ct))
            throw new InvalidOperationException("Email or username is already in use.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.ID == r.UserId, ct) ?? throw new KeyNotFoundException("User was not found.");
        user.FirstName = r.FirstName.Trim(); user.LastName = r.LastName.Trim(); user.UserName = userName; user.Email = email; user.Bio = r.Bio?.Trim(); user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, null, "AdminUserUpdated", nameof(User), user.ID, "SuperAdmin updated a user account.", new Dictionary<string, object?> { ["userName"] = user.UserName, ["email"] = user.Email }), ct);
        return await db.Users.AsNoTracking().Where(x => x.ID == user.ID).Select(x => new AdminUserDetails(x.ID, x.FirstName, x.LastName, x.UserName, x.Email, x.Bio, x.AvatarUrl, x.IsSuspended, x.SuspensionReason, x.UserRoles.Select(ur => ur.Role.Name).ToList(), x.ProjectMembers.Count, x.CreatedAt, x.LastSeen)).SingleAsync(ct);
    }
}
public sealed class DeleteAdminUserHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<DeleteAdminUserCommand>
{
    public async Task Handle(DeleteAdminUserCommand r, CancellationToken ct)
    {
        if (!await AdminAccess.IsSuperAdmin(db, current.UserId, ct)) throw new UnauthorizedAccessException("Only SuperAdmin may delete users.");
        if (r.UserId == current.UserId) throw new InvalidOperationException("You cannot delete your own account.");
        if (string.IsNullOrWhiteSpace(r.Reason)) throw new InvalidOperationException("A deletion reason is required.");
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.ID == r.UserId && !x.IsDeleted, ct) ?? throw new KeyNotFoundException("User was not found.");
        if (user.UserRoles.Any(x => x.Role.Name == SystemRoles.SuperAdmin))
        {
            var superAdminCount = await db.UserRoles.CountAsync(x => x.Role.Name == SystemRoles.SuperAdmin && !x.User.IsDeleted, ct);
            if (superAdminCount <= 1) throw new InvalidOperationException("The last SuperAdmin cannot be deleted.");
        }
        var deletedAt = DateTime.UtcNow;
        var deletedIdentity = user.ID.ToString("N");
        user.IsDeleted = true;
        user.DeletedAt = deletedAt;
        user.IsSuspended = true;
        user.SuspendedAt = deletedAt;
        user.SuspensionReason = r.Reason.Trim();
        user.UpdatedAt = deletedAt;
        user.FirstName = "Deleted";
        user.LastName = "User";
        user.UserName = $"deleted-{deletedIdentity}";
        user.Email = $"deleted-{deletedIdentity}@invalid.local";
        user.PasswordHash = string.Empty;
        user.AvatarUrl = null;
        user.Bio = null;
        user.EmailVerifiedAt = null;
        await db.Conversations
            .Where(conversation =>
                conversation.Type == Coding.Enums.ConversationType.Direct &&
                conversation.Participants.Any(participant => participant.UserId == user.ID))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(conversation => conversation.IsDeleted, true)
                .SetProperty(conversation => conversation.DeletedAt, deletedAt)
                .SetProperty(conversation => conversation.UpdateAt, deletedAt)
                .SetProperty(conversation => conversation.UpdatedAt, deletedAt), ct);
        await db.RefreshTokens.Where(x => x.UserId == user.ID && !x.IsRevoked).ExecuteUpdateAsync(x => x.SetProperty(t => t.IsRevoked, true), ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, null, "AdminUserDeleted", nameof(User), user.ID, "SuperAdmin soft-deleted and anonymized a user account.", new Dictionary<string, object?> { ["reason"] = r.Reason }), ct);
    }
}
public sealed class GetAdminProjectsHandler(AppDbContext db) : IRequestHandler<GetAdminProjectsQuery, PageResult<AdminProjectItem>>
{
    public async Task<PageResult<AdminProjectItem>> Handle(GetAdminProjectsQuery r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 100); var query = db.Projects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(r.Search)) { var p = $"%{r.Search.Trim()}%"; query = query.Where(x => EF.Functions.ILike(x.Name, p)); }
        var total = await query.CountAsync(ct); var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size).Select(x => new AdminProjectItem(x.ID, x.Name, x.Owner.FirstName + " " + x.Owner.LastName, x.IsPublic, x.Members.Count, x.Tasks.Count, x.CreatedAt)).ToListAsync(ct);
        return new(items, total, page, size);
    }
}
public sealed class DeleteAbusiveProjectHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<DeleteAbusiveProjectCommand>
{
    public async Task Handle(DeleteAbusiveProjectCommand r, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(x => x.ID == r.ProjectId, ct) ?? throw new KeyNotFoundException("Project was not found.");
        project.IsDeleted = true; project.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, project.ID, "AdminProjectDeleted", nameof(Project), project.ID, "Administrator soft-deleted an abusive project.", new Dictionary<string, object?> { ["reason"] = r.Reason }), ct);
    }
}
public sealed class GetPlatformStatisticsHandler(AppDbContext db) : IRequestHandler<GetPlatformStatisticsQuery, PlatformStatistics>
{
    public async Task<PlatformStatistics> Handle(GetPlatformStatisticsQuery r, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        return new(await db.Users.CountAsync(x => !x.IsDeleted, ct), await db.Users.CountAsync(x => !x.IsDeleted && x.LastSeen >= since, ct), await db.Users.CountAsync(x => x.IsSuspended, ct), await db.Projects.CountAsync(ct), await db.Projects.CountAsync(x => x.CreatedAt >= since, ct), await db.ActivityLogs.CountAsync(x => x.CreatedAt >= since, ct));
    }
}
