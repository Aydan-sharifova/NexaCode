namespace Coding.Infrastructure.Authentication;

public sealed class ResendSettings
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string ClientBaseUrl { get; set; } = "http://localhost:5173";
}
