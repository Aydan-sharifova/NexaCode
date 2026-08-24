using System.Text;
using Coding.Api.Infrastructure;
using Coding.Application.Abstractions;
using Coding.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Coding.Controllers;

[ApiController, Route("api/billing")]
public sealed class BillingController(StripeBillingService billing, ICurrentUser currentUser) : ControllerBase
{
    [Authorize, HttpGet("status")]
    public Task<BillingStatusDto> Status(CancellationToken ct) => billing.GetStatusAsync(currentUser.UserId, ct);

    [Authorize, HttpPost("checkout")]
    public Task<BillingRedirectDto> Checkout(CancellationToken ct) => billing.CreateCheckoutAsync(currentUser.UserId, ct);

    [Authorize, HttpPost("portal")]
    public Task<BillingRedirectDto> Portal(CancellationToken ct) => billing.CreatePortalAsync(currentUser.UserId, ct);

    [AllowAnonymous, HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        try
        {
            await billing.ProcessWebhookAsync(json, Request.Headers["Stripe-Signature"].ToString(), ct);
            return Ok(new { received = true });
        }
        catch (StripeException)
        {
            return BadRequest(new { error = "Invalid Stripe webhook signature." });
        }
    }
}
