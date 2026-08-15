using System.ComponentModel.DataAnnotations;
using Coding.Exceptions;
using Coding.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController]
[Route("api/dev/email")]
[Authorize(Roles = "SuperAdmin,Admin")]
public sealed class DevelopmentEmailController(
    IEmailSender emailSender,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("test")]
    public async Task<IActionResult> SendTestEmail(
        SendTestEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();

        try
        {
            await emailSender.SendAsync(
                request.Email.Trim(),
                "NexaCode SMTP Test",
                "<p>NexaCode SMTP integration works.</p>",
                cancellationToken);
            return Ok(new { message = "The SMTP provider accepted the test email." });
        }
        catch (EmailDeliveryException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email delivery unavailable",
                detail: "The email could not be delivered at this time.");
        }
    }
}

public sealed record SendTestEmailRequest([property: Required, EmailAddress] string Email);
