using Coding.Data;
using Coding.Exceptions;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Coding.Infrastructure.Billing;

public sealed record BillingStatusDto(string Plan, string Status, bool IsPlus, string? CustomerId);
public sealed record BillingRedirectDto(string Url);

public sealed class StripeBillingService(AppDbContext db, IOptions<StripeBillingSettings> options)
{
    private readonly StripeBillingSettings settings = options.Value;

    public async Task<BillingStatusDto> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.ID == userId, ct);
        return ToStatus(user);
    }

    public async Task<BillingRedirectDto> CreateCheckoutAsync(Guid userId, CancellationToken ct)
    {
        EnsureConfigured();
        var user = await db.Users.SingleAsync(x => x.ID == userId, ct);
        if (user.SubscriptionPlan == "Plus" && user.SubscriptionStatus is "active" or "trialing")
            return await CreatePortalAsync(userId, ct);

        StripeConfiguration.ApiKey = settings.SecretKey;
        var request = new SessionCreateOptions
        {
            Mode = "subscription",
            PaymentMethodTypes = ["card"],
            SuccessUrl = $"{settings.ClientBaseUrl.TrimEnd('/')}/billing?checkout=success",
            CancelUrl = $"{settings.ClientBaseUrl.TrimEnd('/')}/billing?checkout=cancelled",
            ClientReferenceId = user.ID.ToString(),
            Customer = user.StripeCustomerId,
            CustomerEmail = user.StripeCustomerId is null ? user.Email : null,
            LineItems = [new SessionLineItemOptions { Price = settings.PlusPriceId, Quantity = 1 }],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["userId"] = user.ID.ToString() }
            },
            Metadata = new Dictionary<string, string> { ["userId"] = user.ID.ToString() }
        };
        var session = await new SessionService().CreateAsync(request, cancellationToken: ct);
        return new BillingRedirectDto(session.Url);
    }

    public async Task<BillingRedirectDto> CreatePortalAsync(Guid userId, CancellationToken ct)
    {
        EnsureConfigured();
        var customerId = await db.Users.Where(x => x.ID == userId).Select(x => x.StripeCustomerId).SingleAsync(ct);
        if (string.IsNullOrWhiteSpace(customerId))
            throw new InvalidOperationException("No Stripe customer exists for this account yet.");

        StripeConfiguration.ApiKey = settings.SecretKey;
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = $"{settings.ClientBaseUrl.TrimEnd('/')}/billing"
            }, cancellationToken: ct);
        return new BillingRedirectDto(session.Url);
    }

    public async Task ProcessWebhookAsync(string json, string signature, CancellationToken ct)
    {
        EnsureConfigured();
        var stripeEvent = EventUtility.ConstructEvent(json, signature, settings.WebhookSecret);
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Session checkout)
                    await ApplyCheckoutAsync(checkout, ct);
                break;
            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Subscription subscription)
                    await ApplySubscriptionAsync(subscription, ct);
                break;
        }
    }

    private async Task ApplyCheckoutAsync(Session session, CancellationToken ct)
    {
        if (!Guid.TryParse(session.ClientReferenceId, out var userId) &&
            (!session.Metadata.TryGetValue("userId", out var raw) || !Guid.TryParse(raw, out userId))) return;
        var user = await db.Users.SingleOrDefaultAsync(x => x.ID == userId, ct);
        if (user is null) return;
        user.StripeCustomerId = session.CustomerId;
        user.StripeSubscriptionId = session.SubscriptionId;
        user.SubscriptionPlan = session.PaymentStatus == "paid" ? "Plus" : user.SubscriptionPlan;
        user.SubscriptionStatus = session.PaymentStatus == "paid" ? "active" : session.PaymentStatus;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplySubscriptionAsync(Subscription subscription, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.StripeSubscriptionId == subscription.Id || x.StripeCustomerId == subscription.CustomerId, ct);
        if (user is null && subscription.Metadata.TryGetValue("userId", out var raw) && Guid.TryParse(raw, out var id))
            user = await db.Users.SingleOrDefaultAsync(x => x.ID == id, ct);
        if (user is null) return;
        user.StripeCustomerId = subscription.CustomerId;
        user.StripeSubscriptionId = subscription.Id;
        user.SubscriptionStatus = subscription.Status;
        user.SubscriptionPlan = subscription.Status is "active" or "trialing" ? "Plus" : "Free";
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private void EnsureConfigured()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.SecretKey)) missing.Add("STRIPE_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(settings.PlusPriceId)) missing.Add("STRIPE_PLUS_PRICE_ID");
        if (string.IsNullOrWhiteSpace(settings.WebhookSecret)) missing.Add("STRIPE_WEBHOOK_SECRET");
        if (missing.Count > 0)
            throw new ServiceUnavailableException($"Stripe billing configuration is missing: {string.Join(", ", missing)}.");
        if (!settings.PlusPriceId.StartsWith("price_", StringComparison.Ordinal))
            throw new ServiceUnavailableException("STRIPE_PLUS_PRICE_ID must be a Stripe Price ID beginning with 'price_'.");
        if (!settings.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            throw new ServiceUnavailableException("STRIPE_WEBHOOK_SECRET must begin with 'whsec_'.");
    }

    private static BillingStatusDto ToStatus(User user) => new(
        user.SubscriptionPlan, user.SubscriptionStatus,
        user.SubscriptionPlan == "Plus" && user.SubscriptionStatus is "active" or "trialing",
        user.StripeCustomerId);
}
