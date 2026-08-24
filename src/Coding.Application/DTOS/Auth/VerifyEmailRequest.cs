using System.ComponentModel.DataAnnotations;

namespace Coding.DTOS.Auth;

public sealed class VerifyEmailRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [EmailAddress]
    public string? Email { get; init; }
}
