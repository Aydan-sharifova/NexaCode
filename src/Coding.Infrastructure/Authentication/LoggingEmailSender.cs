using Coding.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Authentication;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        this.logger = logger;
    }

    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Email delivery provider is disabled; an email was not delivered.");
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("Email delivery provider is disabled; a verification email was not delivered.");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("Email delivery provider is disabled; a password reset email was not delivered.");
        return Task.CompletedTask;
    }
}
