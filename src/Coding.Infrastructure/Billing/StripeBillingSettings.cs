namespace Coding.Infrastructure.Billing;

public sealed class StripeBillingSettings
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PlusPriceId { get; set; } = string.Empty;
    public string ClientBaseUrl { get; set; } = "http://localhost:5173";
}
