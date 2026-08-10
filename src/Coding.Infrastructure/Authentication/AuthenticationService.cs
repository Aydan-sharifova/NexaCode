using Coding.Data;
using Coding.DTOS.Auth;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using Coding.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Demo;

namespace Coding.Infrastructure.Authentication;

public sealed class AuthenticationService(
    AppDbContext context,
    IEmailSender emailSender,
    IConfiguration configuration,
    IdentityPasswordService passwordService,
    IActivityLogger activityLogger,
    IHttpContextAccessor httpContextAccessor,
    IDemoEnvironmentService demoEnvironment) : IAuthenticationService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan AccountTokenLifetime = TimeSpan.FromHours(1);

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var userName = request.UserName.Trim();

        if (await context.Users.AnyAsync(
                user => user.Email.ToLower() == email || user.UserName.ToLower() == userName.ToLower(),
                cancellationToken))
        {
            throw new ConflictException("An account with that email or username already exists.");
        }

        var guestRole = await context.Roles.SingleAsync(
            role => role.Name == SystemRoles.User,
            cancellationToken);

        var now = DateTime.UtcNow;
        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            UserName = userName,
            Email = email,
            PasswordHash = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            LastSeen = now,
            UserRoles = [],
            RefreshTokens = [],
            AccountTokens = []
        };
        user.PasswordHash = passwordService.Hash(user, request.Password);

        user.UserRoles.Add(new UserRole { Role = guestRole, User = user });

        var verification = CreateAccountToken(user, AccountTokenType.EmailVerification);
        user.AccountTokens.Add(verification.Entity);

        var executionStrategy = context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                context.Users.Add(user);
                await context.SaveChangesAsync(cancellationToken);

                await emailSender.SendEmailVerificationAsync(
                    user.Email,
                    verification.PlainTextToken,
                    cancellationToken);

                var response = await IssueTokensAsync(
                    user,
                    [SystemRoles.User],
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return response;
            }
            catch (EmailDeliveryException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                throw new ServiceUnavailableException(
                    "The verification email could not be sent. No account was created. Please try again later.",
                    exception);
            }
        });
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await context.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Email.ToLower() == email && !item.IsDeleted, cancellationToken);

        if (user is null || !passwordService.Verify(user, request.Password))
            throw new UnauthorizedException("Invalid email or password.");
        if (user.IsSuspended)
            throw new UnauthorizedException("This account has been suspended. Contact support.");

        user.LastSeen = DateTime.UtcNow;
        var roles = user.UserRoles.Select(item => item.Role.Name).Distinct().ToArray();
        var response = await IssueTokensAsync(user, roles, cancellationToken);
        await activityLogger.LogAsync(new(user.ID, null, "Login", nameof(User), user.ID, "User signed in."), cancellationToken);
        return response;
    }

    public async Task<AuthResponse> DemoLoginAsync(
        DemoLoginRequest request,
        CancellationToken cancellationToken)
    {
        demoEnvironment.EnsureAvailable();
        if (!Enum.TryParse<DemoRole>(request.Role, true, out var demoRole))
            throw new UnauthorizedException("Choose Owner, Admin, or Member for demo access.");

        var userId = demoEnvironment.GetUserId(demoRole);
        var user = await context.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(
                item => item.ID == userId && !item.IsDeleted,
                cancellationToken)
            ?? throw new ServiceUnavailableException(
                "The demo is being prepared. Run the demo seed command and try again.");

        var projectRole = await context.ProjectMembers
            .Where(member =>
                member.ProjectId == demoEnvironment.SampleProjectId &&
                member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (!projectRole.HasValue ||
            !string.Equals(projectRole.Value.ToString(), demoRole.ToString(), StringComparison.Ordinal))
            throw new ServiceUnavailableException(
                "The selected demo persona is not configured correctly. Reset the demo data.");

        user.LastSeen = DateTime.UtcNow;
        var roles = user.UserRoles.Select(item => item.Role.Name).Distinct().ToArray();
        var response = await IssueTokensAsync(
            user,
            roles,
            cancellationToken,
            demoRole);
        await activityLogger.LogAsync(
            new(
                user.ID,
                demoEnvironment.SampleProjectId,
                "DemoLogin",
                nameof(User),
                user.ID,
                $"Public demo signed in as {demoRole}."),
            cancellationToken);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await context.RefreshTokens
            .Include(item => item.User)
            .ThenInclude(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Token == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpireDate <= DateTime.UtcNow)
            throw new UnauthorizedException("The refresh token is invalid or expired.");
        if (storedToken.User.IsSuspended)
            throw new UnauthorizedException("This account has been suspended.");

        storedToken.IsRevoked = true;
        storedToken.UpdateAt = DateTime.UtcNow;

        var roles = storedToken.User.UserRoles.Select(item => item.Role.Name).Distinct().ToArray();
        return await IssueTokensAsync(
            storedToken.User,
            roles,
            cancellationToken,
            demoEnvironment.TryGetRole(storedToken.User.ID, out var demoRole)
                ? demoRole
                : null);
    }

    public async Task RevokeAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var token = await context.RefreshTokens.SingleOrDefaultAsync(
            item => item.Token == tokenHash,
            cancellationToken);

        if (token is null) return;

        token.IsRevoked = true;
        token.UpdateAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync(new(token.UserId, null, "Logout", nameof(User), token.UserId, "User signed out."), cancellationToken);
    }

    public async Task RequestEmailVerificationAsync(
        EmailRequest request,
        CancellationToken cancellationToken)
    {
        var user = await FindUserByEmailAsync(request.Email, cancellationToken);
        if (user is null || user.EmailVerifiedAt.HasValue) return;

        await InvalidateAccountTokensAsync(user.ID, AccountTokenType.EmailVerification, cancellationToken);
        var token = CreateAccountToken(user, AccountTokenType.EmailVerification);
        context.AccountTokens.Add(token.Entity);
        await context.SaveChangesAsync(cancellationToken);
        await emailSender.SendEmailVerificationAsync(user.Email, token.PlainTextToken, cancellationToken);
    }

    public async Task VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var token = await GetValidAccountTokenAsync(
            request.Token,
            AccountTokenType.EmailVerification,
            cancellationToken);

        token.ConsumedAt = DateTime.UtcNow;
        token.User.EmailVerifiedAt = DateTime.UtcNow;
        token.User.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(
        EmailRequest request,
        CancellationToken cancellationToken)
    {
        var user = await FindUserByEmailAsync(request.Email, cancellationToken);
        if (user is null) return;

        await InvalidateAccountTokensAsync(user.ID, AccountTokenType.PasswordReset, cancellationToken);
        var token = CreateAccountToken(user, AccountTokenType.PasswordReset);
        context.AccountTokens.Add(token.Entity);
        await context.SaveChangesAsync(cancellationToken);
        await emailSender.SendPasswordResetAsync(user.Email, token.PlainTextToken, cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var token = await GetValidAccountTokenAsync(
            request.Token,
            AccountTokenType.PasswordReset,
            cancellationToken);

        token.ConsumedAt = DateTime.UtcNow;
        token.User.PasswordHash = passwordService.Hash(token.User, request.NewPassword);
        token.User.UpdatedAt = DateTime.UtcNow;

        await context.RefreshTokens
            .Where(item => item.UserId == token.UserId && !item.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRevoked, true), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(
        User user,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken,
        DemoRole? demoRole = null)
    {
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var expiresAt = now.AddMinutes(
            demoRole.HasValue
                ? demoEnvironment.AccessTokenMinutes
                : configuration.GetValue("Jwt:AccessTokenMinutes", 15));
        var key = configuration["Jwt:Key"]!;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.ID.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ,new("sid", sessionId.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (demoRole.HasValue)
        {
            claims.Add(new Claim("demo", "true"));
            claims.Add(new Claim("demo_role", demoRole.Value.ToString()));
            claims.Add(new Claim("demo_project_id", demoEnvironment.SampleProjectId.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            claims,
            now,
            expiresAt,
            credentials);

        var plainRefreshToken = GenerateToken();
        var refreshToken = new RefreshToken
        {
            ID = Guid.NewGuid(),
            UserId = user.ID,
            Token = HashToken(plainRefreshToken),
            ExpireDate = demoRole.HasValue
                ? now.AddHours(demoEnvironment.RefreshTokenHours)
                : now.Add(RefreshTokenLifetime),
            CreatAt = now
        };
        context.RefreshTokens.Add(refreshToken);
        var request = httpContextAccessor.HttpContext;
        context.UserSessions.Add(new UserSession
        {
            Id = sessionId, UserId = user.ID, RefreshToken = refreshToken,
            CreatedAt = now, LastSeenAt = now, ExpiresAt = refreshToken.ExpireDate,
            IpAddress = request?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request?.Request.Headers.UserAgent.ToString()
        });
        await context.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            plainRefreshToken,
            expiresAt,
            new AuthenticatedUser(
                user.ID,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.Email,
                user.EmailVerifiedAt.HasValue,
                roles,
                demoRole.HasValue,
                demoRole?.ToString(),
                demoRole.HasValue ? demoEnvironment.SampleProjectId : null));
    }

    private async Task<AccountToken> GetValidAccountTokenAsync(
        string plainToken,
        AccountTokenType type,
        CancellationToken cancellationToken)
    {
        var hash = HashToken(plainToken);
        var token = await context.AccountTokens
            .Include(item => item.User)
            .SingleOrDefaultAsync(item =>
                item.TokenHash == hash &&
                item.Type == type &&
                item.ConsumedAt == null &&
                item.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        return token ?? throw new UnauthorizedException("The token is invalid or expired.");
    }

    private async Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        await context.Users.SingleOrDefaultAsync(
            user => user.Email.ToLower() == NormalizeEmail(email) && !user.IsDeleted,
            cancellationToken);

    private async Task InvalidateAccountTokensAsync(
        Guid userId,
        AccountTokenType type,
        CancellationToken cancellationToken) =>
        await context.AccountTokens
            .Where(item => item.UserId == userId && item.Type == type && item.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ConsumedAt, DateTime.UtcNow),
                cancellationToken);

    private static (AccountToken Entity, string PlainTextToken) CreateAccountToken(
        User user,
        AccountTokenType type)
    {
        var plainText = GenerateToken();
        return (new AccountToken
        {
            User = user,
            UserId = user.ID,
            Type = type,
            TokenHash = HashToken(plainText),
            ExpiresAt = DateTime.UtcNow.Add(AccountTokenLifetime)
        }, plainText);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string GenerateToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
