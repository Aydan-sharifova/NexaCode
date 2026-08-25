using Coding.Infrastructure.Authentication;
using Coding.Application.Features.Demo;
using Coding.Application.Features.Users;
using Coding.Infrastructure.Demo;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coding.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        bool seedDevelopmentData = false,
        bool seedDemoData = false,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        var existingRoles = await context.Roles
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);

        foreach (var roleName in SystemRoles.All.Except(existingRoles, StringComparer.OrdinalIgnoreCase))
        {
            context.Roles.Add(new Role
            {
                Name = roleName,
                Description = $"Built-in {roleName} role."
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        if (seedDevelopmentData)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }

        if (seedDemoData)
        {
            var demoEnvironment =
                scope.ServiceProvider.GetRequiredService<IDemoEnvironmentService>();
            demoEnvironment.EnsureAvailable();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
    }

    public static async Task ResetDemoEnvironmentAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var demoEnvironment =
            scope.ServiceProvider.GetRequiredService<IDemoEnvironmentService>();
        await demoEnvironment.ResetAsync(cancellationToken);
    }

    public static async Task BootstrapProductionAdminAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IdentityPasswordService>();
        var publicIds = scope.ServiceProvider.GetRequiredService<IPublicUserIdGenerator>();

        var email = Required("AdminBootstrap:Email").Trim().ToLowerInvariant();
        var password = Required("AdminBootstrap:Password");
        var userName = Required("AdminBootstrap:UserName").Trim();
        var firstName = configuration["AdminBootstrap:FirstName"]?.Trim() ?? "Platform";
        var lastName = configuration["AdminBootstrap:LastName"]?.Trim() ?? "Administrator";

        if (password.Length < 16 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            password.All(char.IsLetterOrDigit))
            throw new InvalidOperationException(
                "AdminBootstrap__Password must contain at least 16 characters, uppercase, lowercase, number, and special character.");

        var requiredRoles = await context.Roles
            .Where(role => role.Name == SystemRoles.SuperAdmin ||
                           role.Name == SystemRoles.Admin ||
                           role.Name == SystemRoles.User)
            .ToListAsync(cancellationToken);
        if (requiredRoles.Count != 3)
            throw new InvalidOperationException("SuperAdmin, Admin, and User roles must exist before bootstrapping an administrator.");

        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(item => item.UserRoles)
            .SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
        var now = DateTime.UtcNow;

        if (user is null)
        {
            if (await context.Users.IgnoreQueryFilters().AnyAsync(item => item.UserName == userName, cancellationToken))
                throw new InvalidOperationException("AdminBootstrap__UserName is already in use.");

            user = new User
            {
                ID = Guid.NewGuid(),
                Email = email,
                UserName = userName,
                PublicId = await publicIds.GenerateAsync(cancellationToken),
                FirstName = firstName,
                LastName = lastName,
                PasswordHash = string.Empty,
                CreatedAt = now,
                UpdatedAt = now,
                LastSeen = now,
                EmailVerifiedAt = now,
                Status = Coding.Enums.UserStatus.Active
            };
            context.Users.Add(user);
        }
        else
        {
            user.FirstName = firstName;
            user.LastName = lastName;
            user.EmailVerifiedAt ??= now;
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.IsSuspended = false;
            user.SuspendedAt = null;
            user.SuspensionReason = null;
            user.Status = Coding.Enums.UserStatus.Active;
            user.TokenVersion++;
            user.UpdatedAt = now;
            await context.RefreshTokens.Where(item => item.UserId == user.ID).ExecuteDeleteAsync(cancellationToken);
        }

        user.PasswordHash = passwords.Hash(user, password);
        foreach (var role in requiredRoles.Where(role => user.UserRoles.All(item => item.RoleId != role.ID)))
            user.UserRoles.Add(new UserRole { ID = Guid.NewGuid(), UserId = user.ID, RoleId = role.ID, Role = role });

        await context.SaveChangesAsync(cancellationToken);

        string Required(string key) =>
            string.IsNullOrWhiteSpace(configuration[key])
                ? throw new InvalidOperationException($"{key.Replace(':', '_')} is required for --bootstrap-admin.")
                : configuration[key]!;
    }
}
