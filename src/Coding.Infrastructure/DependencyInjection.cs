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
using Coding.Application.Features.Runtime;
using Coding.Infrastructure.Runtime;
using Coding.Infrastructure.AiAgent;
using Coding.Infrastructure.Caching;
using Coding.Application.Abstractions;
using Coding.Application.Features.Demo;
using Coding.Infrastructure.Demo;
using Coding.Application.Features.Collaboration;
using Coding.Infrastructure.Collaboration;
using Coding.Application.Features.Users;
using Coding.Infrastructure.Users;
using Coding.Application.Features.Deployments;
using Coding.Infrastructure.Deployments;
using Coding.Application.Features.Repositories;
using Coding.Infrastructure.Repositories;
using Coding.Infrastructure.Kanban;
using Coding.Infrastructure.Projects;
using Coding.Application.Features.Marketplace;
using Coding.Infrastructure.Marketplace;
using Coding.Application.Features.Achievements;
using Coding.Infrastructure.Achievements;
using Coding.Application.Features.Mentor;
using Coding.Application.Features.ProjectPlanner;
using Coding.Application.Features.KnowledgeGraph;
using Coding.Infrastructure.KnowledgeGraph;
using Coding.Application.Features.Debugging;
using Coding.Infrastructure.Debugging;
using Coding.Application.Features.AutonomousTesting;
using Coding.Infrastructure.AutonomousTesting;
using Coding.Application.Features.ScreenshotToCode;
using Coding.Infrastructure.ScreenshotToCode;
using Coding.Application.Features.AiUiGenerator;
using Coding.Infrastructure.AiUiGenerator;
using Coding.Infrastructure.Billing;

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
        services.AddOptions<StripeBillingSettings>()
            .Bind(configuration.GetSection(StripeBillingSettings.SectionName));
        services.AddScoped<StripeBillingService>();

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
        services.AddSingleton<IProjectRepositoryCoordinator, ProjectRepositoryCoordinator>();
        services.AddSingleton<IGitRepositoryService, NativeGitRepositoryService>();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<SmtpSettings>, SmtpSettingsValidator>();
        services.AddOptions<SmtpSettings>()
            .Bind(configuration.GetSection(SmtpSettings.SectionName))
            .ValidateOnStart();
        services.AddOptions<ResendSettings>()
            .Bind(configuration.GetSection(ResendSettings.SectionName))
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ApiKey) ||
                    (!string.IsNullOrWhiteSpace(options.FromEmail) &&
                     Uri.TryCreate(options.ClientBaseUrl, UriKind.Absolute, out _)),
                "Configured Resend requires a from email and an absolute client base URL.")
            .ValidateOnStart();
        services.AddScoped<LoggingEmailSender>();
        services.AddScoped<SmtpEmailSender>();
        services.AddHttpClient<ResendEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IEmailSender>(provider =>
        {
            if (!string.IsNullOrWhiteSpace(provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ResendSettings>>()
                    .Value.ApiKey))
                return provider.GetRequiredService<ResendEmailSender>();

            return provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpSettings>>().Value.Enabled
                ? provider.GetRequiredService<SmtpEmailSender>()
                : provider.GetRequiredService<LoggingEmailSender>();
        });
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
        services.AddHostedService<TaskDeadlineMonitorService>();
        services.AddHostedService<ProjectDeadlineMonitorService>();
        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<IProjectDeploymentService, ProjectDeploymentService>();
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
            services.AddScoped<IDatabaseMetadataProvider, EfCoreDatabaseMetadataProvider>();
        services.AddOptions<AiProviderOptions>()
            .Bind(configuration.GetSection(AiProviderOptions.SectionName));
        services.AddOptions<OpenAiCompatibleOptions>()
            .Bind(configuration.GetSection(OpenAiCompatibleOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
                           !string.IsNullOrWhiteSpace(options.Model),
                "OpenAICompatible requires a valid BaseUrl and Model.")
            .ValidateOnStart();
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
        services.AddScoped<IMentorService, MentorService>();
        services.AddScoped<IProjectPlannerService, ProjectPlannerService>();
        services.AddScoped<IKnowledgeGraphService, KnowledgeGraphService>();
        services.AddScoped<IDebuggingTimelineService, DebuggingTimelineService>();
        services.AddScoped<IAutonomousTestAgentService, AutonomousTestAgentService>();
        services.AddScoped<IScreenshotToCodeService, ScreenshotToCodeService>();
        services.AddScoped<IAiUiGeneratorService, AiUiGeneratorService>();
        services.AddOptions<ContainerRuntimeOptions>()
            .Bind(configuration.GetSection(ContainerRuntimeOptions.SectionName))
            .Validate(options => !options.Enabled ||
                (System.Text.RegularExpressions.Regex.IsMatch(options.DotNetImage ?? string.Empty, "^[A-Za-z0-9][A-Za-z0-9._/@:-]{0,255}$") &&
                 options.MaximumOutputCharacters is >= 1_024 and <= 65_536),
                "Enabled execution requires a safe container image reference and an output limit between 1,024 and 65,536 characters.")
            .ValidateOnStart();
        services.AddSingleton<IRuntimeProvider, ContainerRuntimeProvider>();
        services.AddScoped<ISocialAccessService, SocialAccessService>();

        // AI agent tool registry, authorization, approval policy, and execution.
        AiAgentServiceRegistration.AddAiAgentServices(services);
        services.AddScoped<IMarketplaceManifestValidator, MarketplaceManifestValidator>();
        services.AddScoped<IAchievementEvaluator, AchievementEvaluator>();
        services.AddHostedService<AchievementBackfillService>();

        return services;
    }
}
