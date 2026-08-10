using System.Net;
using Coding.Services.Interfaces;
using Coding.Exceptions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Coding.Infrastructure.Authentication;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string email, string token, CancellationToken ct) =>
        SendAsync(email, "Verify your Coding email", "Verify email", "Confirm your email address to activate all workspace features.", $"/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}", ct);

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct) =>
        SendAsync(email, "Reset your Coding password", "Reset password", "Use this secure link to choose a new password. If you did not request this, ignore the message.", $"/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}", ct);

    private async Task SendAsync(string recipient, string subject, string heading, string description, string path, CancellationToken ct)
    {
        var settings = options.Value; var link = $"{settings.ClientBaseUrl.TrimEnd('/')}{path}";
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = Template(heading, description, link) }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15_000 };
        try
        {
            var socketOptions = settings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
            logger.LogInformation("Sending {EmailType} email through configured SMTP host {Host}.", heading, settings.Host);
            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, ct);
            await client.AuthenticateAsync(settings.Username, settings.Password, ct);
            await client.SendAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "SMTP delivery failed through host {Host}.", settings.Host);
            throw new EmailDeliveryException("The configured SMTP provider could not deliver the email.", exception);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, CancellationToken.None);
        }
    }

    private static string Template(string heading, string description, string link) => $"""
        <!doctype html><html><body style="margin:0;background:#f4f6fb;font-family:Arial,sans-serif;color:#152039">
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:32px 16px">
        <table role="presentation" width="560" cellspacing="0" cellpadding="0" style="max-width:100%;background:white;border:1px solid #e2e6ef;border-radius:14px">
        <tr><td style="padding:32px"><div style="font-size:20px;font-weight:800;color:#6256e8">NexaCode</div>
        <h1 style="font-size:24px;margin:28px 0 12px">{WebUtility.HtmlEncode(heading)}</h1><p style="color:#667085;line-height:1.6">{WebUtility.HtmlEncode(description)}</p>
        <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;margin-top:16px;padding:13px 20px;border-radius:8px;background:#6256e8;color:white;text-decoration:none;font-weight:700">{WebUtility.HtmlEncode(heading)}</a>
        <p style="margin-top:28px;color:#98a2b3;font-size:12px;word-break:break-all">If the button does not work, open: {WebUtility.HtmlEncode(link)}</p></td></tr></table>
        </td></tr></table></body></html>
        """;
}
