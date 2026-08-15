using Coding.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class SmtpSettingsValidatorTests
{
    private readonly SmtpSettingsValidator validator = new();

    [Fact]
    public void Disabled_smtp_does_not_require_credentials()
    {
        var result = validator.Validate(null, new SmtpSettings { Enabled = false });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Enabled_gmail_starttls_configuration_is_valid()
    {
        var result = validator.Validate(null, new SmtpSettings
        {
            Enabled = true,
            Host = "smtp.gmail.com",
            Port = 587,
            UseSsl = false,
            UseStartTls = true,
            Username = "sender@example.com",
            Password = "app-password-from-secret-store",
            FromEmail = "sender@example.com",
            FromName = "NexaCode",
            ClientBaseUrl = "http://localhost:5173"
        });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Enabled_smtp_rejects_ssl_on_connect_and_starttls_together()
    {
        var result = validator.Validate(null, new SmtpSettings
        {
            Enabled = true,
            Host = "smtp.gmail.com",
            Port = 587,
            UseSsl = true,
            UseStartTls = true,
            Username = "sender@example.com",
            Password = "app-password-from-secret-store",
            FromEmail = "sender@example.com",
            FromName = "NexaCode",
            ClientBaseUrl = "http://localhost:5173"
        });

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(message => message.Contains("cannot both be enabled"));
    }

    [Fact]
    public void Enabled_smtp_reports_every_invalid_required_setting()
    {
        var result = validator.Validate(null, new SmtpSettings
        {
            Enabled = true,
            Port = 0,
            FromEmail = "not-an-email",
            ClientBaseUrl = "file:///tmp/client"
        });

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(message => message.Contains("Host"));
        result.Failures.Should().Contain(message => message.Contains("Port"));
        result.Failures.Should().Contain(message => message.Contains("Username"));
        result.Failures.Should().Contain(message => message.Contains("Password"));
        result.Failures.Should().Contain(message => message.Contains("FromEmail"));
        result.Failures.Should().Contain(message => message.Contains("ClientBaseUrl"));
    }
}
