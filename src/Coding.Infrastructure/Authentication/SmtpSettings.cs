namespace Coding.Infrastructure.Authentication;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "NexaCode";
    public bool UseSsl { get; set; }
    public bool UseStartTls { get; set; } = true;
    public bool CheckCertificateRevocation { get; set; } = true;
    public string ClientBaseUrl { get; set; } = "http://localhost:5173";
}
