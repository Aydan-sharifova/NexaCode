using Coding.Exceptions;
using Coding.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Coding.UnitTests;

public sealed class LoggingEmailSenderTests
{
    private readonly LoggingEmailSender sender = new(NullLogger<LoggingEmailSender>.Instance);

    [Fact]
    public async Task Verification_email_fails_when_delivery_is_disabled()
    {
        var action = () => sender.SendEmailVerificationAsync(
            "user@example.com",
            "token",
            CancellationToken.None);

        await action.Should().ThrowAsync<EmailDeliveryException>()
            .WithMessage("Email delivery is not configured.");
    }

    [Fact]
    public async Task Password_reset_email_fails_when_delivery_is_disabled()
    {
        var action = () => sender.SendPasswordResetAsync(
            "user@example.com",
            "token",
            CancellationToken.None);

        await action.Should().ThrowAsync<EmailDeliveryException>()
            .WithMessage("Email delivery is not configured.");
    }
}
