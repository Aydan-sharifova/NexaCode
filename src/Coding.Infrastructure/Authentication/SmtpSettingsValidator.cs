using Microsoft.Extensions.Options;
using MimeKit;

namespace Coding.Infrastructure.Authentication;

public sealed class SmtpSettingsValidator : IValidateOptions<SmtpSettings>
{
    public ValidateOptionsResult Validate(string? name, SmtpSettings settings)
    {
        if (!settings.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.Host)) failures.Add("Smtp:Host is required.");
        if (settings.Port is <= 0 or > 65535) failures.Add("Smtp:Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(settings.Username)) failures.Add("Smtp:Username is required.");
        if (string.IsNullOrWhiteSpace(settings.Password)) failures.Add("Smtp:Password is required.");
        if (settings.UseSsl && settings.UseStartTls)
            failures.Add("Smtp:UseSsl and Smtp:UseStartTls cannot both be enabled.");
        if (string.IsNullOrWhiteSpace(settings.FromName)) failures.Add("Smtp:FromName is required.");
        if (string.IsNullOrWhiteSpace(settings.FromEmail) ||
            !MailboxAddress.TryParse(settings.FromEmail, out var fromAddress) ||
            !string.Equals(fromAddress.Address, settings.FromEmail.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !fromAddress.Address.Contains('@') ||
            fromAddress.Address.EndsWith('@'))
            failures.Add("Smtp:FromEmail must be a valid email address.");
        if (!Uri.TryCreate(settings.ClientBaseUrl, UriKind.Absolute, out var clientBaseUri) ||
            (!string.Equals(clientBaseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(clientBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            failures.Add("Smtp:ClientBaseUrl must be an absolute HTTP or HTTPS URL.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
