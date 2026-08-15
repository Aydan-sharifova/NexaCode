using FluentValidation;
using MediatR;

namespace Coding.Application.Features.UserSettings;

public sealed record UserProfileDto(Guid Id, string PublicId, string FirstName, string LastName, string UserName, string Email, string? Bio, string? AvatarUrl);
public sealed record UserPreferenceDto(string Theme, string Language, bool ReducedMotion, bool CompactMode, bool SecurityAlertsEnabled);
public sealed record UserSessionDto(Guid Id, string? IpAddress, string Device, DateTime CreatedAt, DateTime LastSeenAt, DateTime ExpiresAt, bool IsCurrent);
public sealed record NotificationPreferenceDto(string Type, bool InAppEnabled, bool EmailEnabled);
public sealed record UserSettingsDto(UserProfileDto Profile, UserPreferenceDto Preferences, IReadOnlyList<NotificationPreferenceDto> Notifications);
public sealed record GetUserSettingsQuery : IRequest<UserSettingsDto>;
public sealed record UpdateProfileCommand(string FirstName, string LastName, string? Bio) : IRequest<UserProfileDto>;
public sealed record UpdatePreferencesCommand(string Theme, string Language, bool ReducedMotion, bool CompactMode, bool SecurityAlertsEnabled) : IRequest<UserPreferenceDto>;
public sealed record UpdateNotificationPreferencesCommand(IReadOnlyList<NotificationPreferenceDto> Preferences) : IRequest;
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, bool RevokeOtherSessions) : IRequest;
public sealed record UploadAvatarCommand(byte[] Content, string ContentType, string Extension) : IRequest<string>;
public sealed record RemoveAvatarCommand : IRequest;
public sealed record GetUserSessionsQuery : IRequest<IReadOnlyList<UserSessionDto>>;
public sealed record RevokeUserSessionCommand(Guid SessionId) : IRequest;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
}

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Bio).MaximumLength(500);
    }
}
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).MinimumLength(10).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]");
    }
}
