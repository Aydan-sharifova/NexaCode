using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coding.Exceptions;
using Coding.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coding.Infrastructure.Authentication;

public sealed class ResendEmailSender(
    HttpClient client,
    IOptions<ResendSettings> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string email, string token, CancellationToken ct)
    {
        var link = BuildClientLink("verify-email", email, token);
        return SendAsync(email, "Confirm your email address", AccountEmailTemplates.Verification(link), ct);
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct)
    {
        var link = BuildClientLink("reset-password", email, token);
        return SendAsync(email, "Reset your Coding password", AccountEmailTemplates.PasswordReset(link), ct);
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = settings.FromEmail,
                to = new[] { to },
                subject,
                html = htmlBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Resend rejected email delivery with status {StatusCode}.", (int)response.StatusCode);
                throw new EmailDeliveryException(
                    "The configured email provider could not deliver the email.",
                    new HttpRequestException($"Resend returned HTTP {(int)response.StatusCode}."));
            }

            logger.LogInformation("Email was accepted by Resend.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (EmailDeliveryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Resend email delivery failed.");
            throw new EmailDeliveryException("The configured email provider could not deliver the email.", exception);
        }
    }

    private string BuildClientLink(string action, string email, string token) =>
        $"{options.Value.ClientBaseUrl.TrimEnd('/')}?accountAction={Uri.EscapeDataString(action)}&email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
}
