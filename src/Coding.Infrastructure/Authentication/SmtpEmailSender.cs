using Coding.Services.Interfaces;
using Coding.Exceptions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;
using System.Text.RegularExpressions;

namespace Coding.Infrastructure.Authentication;

public sealed class SmtpEmailSender(IOptions<SmtpSettings> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string email, string token, CancellationToken ct)
    {
        var link = BuildClientLink("/verify-email", email, token);
        return SendAsync(email, "Confirm your email address", AccountEmailTemplates.Verification(link), ct);
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct)
    {
        var link = BuildClientLink("/reset-password", email, token);
        return SendAsync(email, "Reset your Coding password", AccountEmailTemplates.PasswordReset(link), ct);
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var settings = options.Value;
        using var client = new SmtpClient
        {
            Timeout = 15_000,
            CheckCertificateRevocation = settings.CheckCertificateRevocation
        };
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
            message.ReplyTo.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = CreatePlainTextBody(htmlBody)
            }.ToMessageBody();

            var socketOptions = settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : settings.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.Auto;
            logger.LogInformation("Sending email through configured SMTP host {Host}.", settings.Host);
            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, ct);
            if (!string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password, ct);
            await client.SendAsync(message, ct);
            logger.LogInformation("Email was accepted by the configured SMTP provider.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SMTP delivery failed through host {Host}.", settings.Host);
            throw new EmailDeliveryException("The configured SMTP provider could not deliver the email.", exception);
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "SMTP disconnect did not complete cleanly.");
                }
            }
        }
    }

    private string BuildClientLink(string path, string email, string token) =>
        $"{options.Value.ClientBaseUrl.TrimEnd('/')}?accountAction={Uri.EscapeDataString(path.TrimStart('/'))}&email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    private static string CreatePlainTextBody(string htmlBody)
    {
        var withLineBreaks = Regex.Replace(htmlBody, "<(br|/p|/div|/h[1-6]|/tr)[^>]*>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, "<[^>]+>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }
}
