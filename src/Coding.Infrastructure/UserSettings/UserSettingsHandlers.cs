using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.UserSettings;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Authentication;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Coding.Application.Features.Demo;

namespace Coding.Infrastructure.UserSettings;

public sealed class GetUserSettingsHandler(AppDbContext db, ICurrentUser current) : IRequestHandler<GetUserSettingsQuery, UserSettingsDto>
{
    public async Task<UserSettingsDto> Handle(GetUserSettingsQuery r, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.ID == current.UserId, ct);
        var preference = await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == current.UserId, ct);
        var notifications = await db.UserNotificationPreferences.AsNoTracking().Where(x => x.UserId == current.UserId).ToListAsync(ct);
        return new(new(user.ID, user.PublicId, user.FirstName, user.LastName, user.UserName, user.Email, user.Bio, user.AvatarUrl),
            new(preference?.Theme ?? "system", preference?.Language ?? "en", preference?.ReducedMotion ?? false, preference?.CompactMode ?? false, preference?.SecurityAlertsEnabled ?? true),
            Enum.GetValues<NotificationType>().Select(type => { var item = notifications.FirstOrDefault(x => x.Type == type); return new NotificationPreferenceDto(type.ToString(), item?.InAppEnabled ?? true, item?.EmailEnabled ?? false); }).ToList());
    }
}
public sealed class UpdateProfileHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(UpdateProfileCommand r, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(x => x.ID == current.UserId, ct); user.FirstName = r.FirstName.Trim(); user.LastName = r.LastName.Trim(); user.Bio = r.Bio?.Trim(); user.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, null, "ProfileUpdated", nameof(User), user.ID, "User updated profile settings."), ct);
        return new(user.ID, user.PublicId, user.FirstName, user.LastName, user.UserName, user.Email, user.Bio, user.AvatarUrl);
    }
}
public sealed class UpdatePreferencesHandler(AppDbContext db, ICurrentUser current) : IRequestHandler<UpdatePreferencesCommand, UserPreferenceDto>
{
    public async Task<UserPreferenceDto> Handle(UpdatePreferencesCommand r, CancellationToken ct)
    {
        if (!new[] { "light", "dark", "system" }.Contains(r.Theme)) throw new InvalidOperationException("Unsupported theme.");
        if (!new[] { "en", "az", "ru", "de", "tr" }.Contains(r.Language.Trim().ToLowerInvariant())) throw new InvalidOperationException("Unsupported language.");
        var item = await db.UserPreferences.SingleOrDefaultAsync(x => x.UserId == current.UserId, ct) ?? new UserPreference { UserId = current.UserId };
        if (db.Entry(item).State == EntityState.Detached) db.UserPreferences.Add(item);
        item.Theme = r.Theme; item.Language = r.Language.Trim().ToLowerInvariant(); item.ReducedMotion = r.ReducedMotion; item.CompactMode = r.CompactMode; item.SecurityAlertsEnabled = r.SecurityAlertsEnabled; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        return new(item.Theme, item.Language, item.ReducedMotion, item.CompactMode, item.SecurityAlertsEnabled);
    }
}
public sealed class UpdateNotificationPreferencesHandler(AppDbContext db, ICurrentUser current) : IRequestHandler<UpdateNotificationPreferencesCommand>
{
    public async Task Handle(UpdateNotificationPreferencesCommand r, CancellationToken ct)
    {
        foreach (var input in r.Preferences)
        {
            if (!Enum.TryParse<NotificationType>(input.Type, true, out var type)) continue;
            var item = await db.UserNotificationPreferences.SingleOrDefaultAsync(x => x.UserId == current.UserId && x.Type == type, ct);
            if (item is null) db.UserNotificationPreferences.Add(new UserNotificationPreference { ID = Guid.NewGuid(), UserId = current.UserId, Type = type, InAppEnabled = input.InAppEnabled, EmailEnabled = input.EmailEnabled });
            else { item.InAppEnabled = input.InAppEnabled; item.EmailEnabled = input.EmailEnabled; }
        }
        await db.SaveChangesAsync(ct);
    }
}
public sealed class ChangePasswordHandler(AppDbContext db, ICurrentUser current, IdentityPasswordService passwords, IActivityLogger audit, IHttpContextAccessor accessor) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand r, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(x => x.ID == current.UserId, ct);
        if (!passwords.Verify(user, r.CurrentPassword)) throw new UnauthorizedAccessException("Current password is incorrect.");
        user.PasswordHash = passwords.Hash(user, r.NewPassword); user.UpdatedAt = DateTime.UtcNow;
        if (r.RevokeOtherSessions)
        {
            var sid = Guid.TryParse(accessor.HttpContext?.User.FindFirst("sid")?.Value, out var parsed) ? parsed : Guid.Empty;
            await db.RefreshTokens.Where(x => x.UserId == user.ID && !x.IsRevoked && !db.UserSessions.Any(s => s.Id == sid && s.RefreshTokenId == x.ID)).ExecuteUpdateAsync(x => x.SetProperty(t => t.IsRevoked, true), ct);
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(current.UserId, null, "PasswordChanged", nameof(User), user.ID, "User changed account password."), ct);
    }
}
public sealed class UploadAvatarHandler(
    AppDbContext db,
    ICurrentUser current,
    IFileStorageService storage,
    IDemoEnvironmentService demoEnvironment) : IRequestHandler<UploadAvatarCommand, string>
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase) { [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp" };
    public async Task<string> Handle(UploadAvatarCommand r, CancellationToken ct)
    {
        demoEnvironment.EnsureFileAllowed(
            current.UserId,
            $"avatar{r.Extension}",
            r.Content.LongLength);
        if (r.Content.Length is 0 or > 5_242_880 ||
            !Allowed.TryGetValue(r.Extension, out var mime) ||
            !string.Equals(mime, r.ContentType, StringComparison.OrdinalIgnoreCase) ||
            !HasValidSignature(r.Content, mime))
            throw new FluentValidation.ValidationException("Avatar must be a valid JPG, PNG, or WebP file up to 5 MB.");
        var user = await db.Users.SingleAsync(x => x.ID == current.UserId, ct); if (!string.IsNullOrWhiteSpace(user.AvatarUrl)) await storage.DeleteAsync(user.AvatarUrl, ct);
        await using var stream = new MemoryStream(r.Content, writable: false); user.AvatarUrl = await storage.SaveAsync(stream, r.Extension.ToLowerInvariant(), r.ContentType, ct); user.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return user.AvatarUrl;
    }

    private static bool HasValidSignature(byte[] content, string mime) => mime switch
    {
        "image/jpeg" => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
        "image/png" => content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/webp" => content.Length >= 12 &&
            content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
public sealed class RemoveAvatarHandler(AppDbContext db, ICurrentUser current, IFileStorageService storage) : IRequestHandler<RemoveAvatarCommand>
{
    public async Task Handle(RemoveAvatarCommand r, CancellationToken ct) { var user = await db.Users.SingleAsync(x => x.ID == current.UserId, ct); if (user.AvatarUrl is not null) await storage.DeleteAsync(user.AvatarUrl, ct); user.AvatarUrl = null; await db.SaveChangesAsync(ct); }
}
public sealed class GetUserSessionsHandler(AppDbContext db, ICurrentUser current, IHttpContextAccessor accessor) : IRequestHandler<GetUserSessionsQuery, IReadOnlyList<UserSessionDto>>
{
    public async Task<IReadOnlyList<UserSessionDto>> Handle(GetUserSessionsQuery r, CancellationToken ct) { var sid = Guid.TryParse(accessor.HttpContext?.User.FindFirst("sid")?.Value, out var parsed) ? parsed : Guid.Empty; return await db.UserSessions.AsNoTracking().Where(x => x.UserId == current.UserId && x.RevokedAt == null && !x.RefreshToken.IsRevoked && x.ExpiresAt > DateTime.UtcNow).OrderByDescending(x => x.LastSeenAt).Select(x => new UserSessionDto(x.Id, x.IpAddress, x.UserAgent ?? "Unknown device", x.CreatedAt, x.LastSeenAt, x.ExpiresAt, x.Id == sid)).ToListAsync(ct); }
}
public sealed class RevokeUserSessionHandler(AppDbContext db, ICurrentUser current, IActivityLogger audit) : IRequestHandler<RevokeUserSessionCommand>
{
    public async Task Handle(RevokeUserSessionCommand r, CancellationToken ct) { var item = await db.UserSessions.Include(x => x.RefreshToken).SingleOrDefaultAsync(x => x.Id == r.SessionId && x.UserId == current.UserId, ct) ?? throw new KeyNotFoundException("Session was not found."); item.RevokedAt = DateTime.UtcNow; item.RefreshToken.IsRevoked = true; await db.SaveChangesAsync(ct); await audit.LogAsync(new(current.UserId, null, "SessionRevoked", nameof(UserSession), item.Id, "User revoked an active session."), ct); }
}
