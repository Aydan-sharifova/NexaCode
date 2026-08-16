using Coding.Data;
using Coding.Services.Interfaces;
using Coding.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Coding.Application.Features.Notifications;
using Coding.Infrastructure.Notifications;
using Coding.Application.Features.Activities;
using Coding.Infrastructure.Activities;
using Coding.Application.Features.UserSettings;
using Coding.Infrastructure.UserSettings;
using Coding.Infrastructure.DatabaseMetadata;
using Coding.Application.Features.DatabaseMetadata;
using Coding.Application.Features.AiAssistant;
using Coding.Infrastructure.AiAssistant;
using Coding.Infrastructure.AiAgent;
using Coding.Infrastructure.Caching;
using Coding.Application.Abstractions;
using Coding.Application.Features.Demo;
using Coding.Infrastructure.Demo;
using Coding.Application.Features.Collaboration;
using Coding.Infrastructure.Collaboration;
using Coding.Application.Features.Users;
using Coding.Infrastructure.Users;
using Coding.Application.Features.Repositories;
using Coding.Infrastructure.Repositories;

namespace Coding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The PostgreSQL connection string 'Default' is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            }));

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgresql", tags: ["ready"]);

        services.AddScoped<ICollaborativeDocumentStore, CollaborativeDocumentStore>();
        services.AddSingleton<CollaborativeContentMaterializer>();
        services.AddSingleton<ICollaborativeContentMaterializer>(provider => provider.GetRequiredService<CollaborativeContentMaterializer>());
        services.AddHostedService(provider => provider.GetRequiredService<CollaborativeContentMaterializer>());
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IPublicUserIdGenerator, PublicUserIdGenerator>();
        services.AddOptions<RepositoryStorageOptions>()
            .Bind(configuration.GetSection(RepositoryStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath), "Repository storage root is required.")
            .ValidateOnStart();
        services.AddSingleton<IGitRepositoryService, NativeGitRepositoryService>();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<SmtpSettings>, SmtpSettingsValidator>();
        services.AddOptions<SmtpSettings>()
            .Bind(configuration.GetSection(SmtpSettings.SectionName))
            .ValidateOnStart();
        services.AddScoped<LoggingEmailSender>();
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<IEmailSender>(provider =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpSettings>>().Value.Enabled
                ? provider.GetRequiredService<SmtpEmailSender>()
                : provider.GetRequiredService<LoggingEmailSender>());
        services.AddScoped<IdentityPasswordService>();
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddOptions<DemoModeOptions>()
            .Bind(configuration.GetSection(DemoModeOptions.SectionName))
            .Validate(
                options =>
                    !options.Enabled ||
                    (!string.IsNullOrWhiteSpace(options.DatabaseNameMarker) &&
                     options.AccessTokenMinutes is >= 5 and <= 60 &&
                     options.RefreshTokenHours is >= 1 and <= 12 &&
                     options.MaxUploadBytes is >= 65_536 and <= 5_242_880),
                "Enabled DemoMode requires safe token lifetimes, upload limits, and a database marker.")
            .ValidateOnStart();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<IDemoEnvironmentService, DemoEnvironmentService>();
        services.AddHostedService<DemoResetBackgroundService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IActivityLogger, ActivityLogger>();
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
            services.AddScoped<IDatabaseMetadataProvider, EfCoreDatabaseMetadataProvider>();
        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddOptions<AiProviderOptions>()
            .Bind(configuration.GetSection(AiProviderOptions.SectionName));
        services.AddOptions<OpenAiCompatibleOptions>()
            .Bind(configuration.GetSection(OpenAiCompatibleOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
                           !string.IsNullOrWhiteSpace(options.Model),
                "OpenAICompatible requires a valid BaseUrl and Model.")
            .ValidateOnStart();
        services.AddHttpClient<OpenAiProvider>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient<OpenAiCompatibleProvider>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddScoped<DevelopmentAiProvider>();
        services.AddScoped<IAiProvider>(provider =>
        {
            var selected = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiProviderOptions>>()
                .Value;
            return selected.Provider.Trim().ToLowerInvariant() switch
            {
                "openai" => provider.GetRequiredService<OpenAiProvider>(),
                "ollama" or "openaicompatible" =>
                    provider.GetRequiredService<OpenAiCompatibleProvider>(),
                "development" => provider.GetRequiredService<DevelopmentAiProvider>(),
                _ => throw new InvalidOperationException(
                    $"Unknown AI provider '{selected.Provider}'.")
            };
        });
        services.AddScoped<IAiContextBuilder, AiContextBuilder>();
        services.AddScoped<IAiPromptTemplateService, AiPromptTemplateService>();
        services.AddScoped<IAiUsageTracker, AiUsageTracker>();
        services.AddScoped<IGuestAiService, GuestAiService>();
        services.AddScoped<IAiConversationService, AiConversationService>();

        // AI agent tool registry, authorization, approval policy, and execution.
        AiAgentServiceRegistration.AddAiAgentServices(services);

        return services;
    }
}
