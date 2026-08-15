namespace Coding.Services.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken);
}
