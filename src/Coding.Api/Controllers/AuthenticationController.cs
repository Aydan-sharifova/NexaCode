using Coding.DTOS.Auth;
using Coding.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Coding.Application.Security;

namespace Coding.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthenticationController : ControllerBase
{
    private const string RefreshTokenCookie = "refresh_token";
    private readonly IAuthenticationService authenticationService;
    private readonly IConfiguration configuration;

    public AuthenticationController(IAuthenticationService authenticationService,IConfiguration configuration)
    {
        this.authenticationService = authenticationService;
        this.configuration=configuration;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [EndpointSummary("Register a new user account")]
    [EndpointDescription("Public account creation endpoint. Creates the user, assigns the Guest role, issues JWT/refresh tokens, and starts email verification.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authenticationService.RegisterAsync(request, cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken);
        return StatusCode(StatusCodes.Status201Created, ToPublicResponse(response));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EndpointSummary("Sign in and receive an access token")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authenticationService.LoginAsync(request, cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(ToPublicResponse(response));
    }

    [AllowAnonymous]
    [HttpPost("demo-login")]
    [EndpointSummary("Enter the isolated public demo as a predefined project role")]
    [EndpointDescription("Available only in the Demo environment. No demo password is exposed to the client.")]
    public async Task<IActionResult> DemoLogin(
        DemoLoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authenticationService.DemoLoginAsync(request, cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(ToPublicResponse(response));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!CookieOriginAllowed()) return Forbid();
        if (!Request.Cookies.TryGetValue(RefreshTokenCookie, out var refreshToken))
            return Unauthorized();

        var response = await authenticationService.RefreshAsync(
            new RefreshTokenRequest { RefreshToken = refreshToken },
            cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(ToPublicResponse(response));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!CookieOriginAllowed()) return Forbid();
        if (Request.Cookies.TryGetValue(RefreshTokenCookie, out var refreshToken))
        {
            await authenticationService.RevokeAsync(
                new RefreshTokenRequest { RefreshToken = refreshToken },
                cancellationToken);
        }

        Response.Cookies.Delete(RefreshTokenCookie, GetRefreshTokenCookieOptions());
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("email-verification/request")]
    public async Task<IActionResult> RequestEmailVerification(
        EmailRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.RequestEmailVerificationAsync(request, cancellationToken);
        return Accepted();
    }

    [AllowAnonymous]
    [HttpPost("email-verification/confirm")]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.VerifyEmailAsync(request, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(
        EmailRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.RequestPasswordResetAsync(request, cancellationToken);
        return Accepted();
    }

    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.ResetPasswordAsync(request, cancellationToken);
        return NoContent();
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookie, token, GetRefreshTokenCookieOptions());
    }

    private CookieOptions GetRefreshTokenCookieOptions()
    {
        var origin = Request.Headers.Origin.ToString();
        var allowedOrigins=configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()??[];
        var crossSiteRequest = Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && !string.Equals(originUri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase)
            && RequestOriginPolicy.IsAllowed(origin,Request.Scheme,Request.Host.Value,allowedOrigins);
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || crossSiteRequest,
            SameSite = crossSiteRequest ? SameSiteMode.None : SameSiteMode.Strict,
            Path = "/api/auth",
            MaxAge = TimeSpan.FromDays(30),
            IsEssential = true
        };
    }

    private bool CookieOriginAllowed()=>RequestOriginPolicy.IsAllowed(Request.Headers.Origin.ToString(),Request.Scheme,Request.Host.Value,configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()??[]);

    private static object ToPublicResponse(AuthResponse response) => new
    {
        response.AccessToken,
        response.AccessTokenExpiresAt,
        response.User
    };
}
