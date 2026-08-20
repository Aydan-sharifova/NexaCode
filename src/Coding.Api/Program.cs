using Coding.Api.Configuration;
using Coding.Data;
using Coding.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Coding.Api.Collaboration;
using Coding.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

try
{
    EnvironmentFile.LoadForDevelopment(Directory.GetCurrentDirectory());
    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environments.Production;
    var workingDirectory = Directory.GetCurrentDirectory();
    var repositoryApiDirectory = Path.Combine(workingDirectory, "src", "Coding.Api");
    var contentRoot = File.Exists(Path.Combine(workingDirectory, "appsettings.json"))
        ? workingDirectory
        : File.Exists(Path.Combine(repositoryApiDirectory, "appsettings.json"))
            ? repositoryApiDirectory
            : AppContext.BaseDirectory;
    var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
    {
        Args = args,
        ApplicationName = typeof(Program).Assembly.FullName,
        ContentRootPath = contentRoot,
        EnvironmentName = environmentName
    });
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
    builder.WebHost.UseKestrel();

    // Render and similar platforms route traffic only to the port they expose.
    // Prefer their PORT value and listen on every container interface. Outside
    // such platforms, Kestrel continues to honor ASPNETCORE_URLS or
    // ASPNETCORE_HTTP_PORTS (including the Docker image's port 8080 default).
    var platformPort = Environment.GetEnvironmentVariable("PORT");
    if (int.TryParse(platformPort, out var parsedPort) &&
        parsedPort is > 0 and <= 65535)
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
    }
    else
    {
        var configuredUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(configuredUrls)
            ? "http://localhost:5192"
            : configuredUrls);
    }

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApiServices(builder.Configuration);

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();
    var migrationOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
    var demoSeedOnly = args.Contains("--demo-seed", StringComparer.OrdinalIgnoreCase);
    var demoResetOnly = args.Contains("--demo-reset", StringComparer.OrdinalIgnoreCase);
    var demoModeEnabled = builder.Configuration.GetValue("DemoMode:Enabled", false);

    if (demoModeEnabled && !app.Environment.IsEnvironment("Demo"))
        throw new InvalidOperationException(
            "DemoMode may be enabled only when ASPNETCORE_ENVIRONMENT is Demo.");

    if (migrationOnly ||
        demoSeedOnly ||
        demoResetOnly ||
        builder.Configuration.GetValue("Database:ApplyMigrations", false))
    {
        await app.Services.InitializeDatabaseAsync(
            seedDevelopmentData: app.Environment.IsDevelopment() &&
                builder.Configuration.GetValue("Database:SeedDevelopmentData", false),
            seedDemoData: demoModeEnabled && !demoResetOnly);
    }

    if (demoResetOnly)
    {
        await app.Services.ResetDemoEnvironmentAsync();
        Log.Information("Demo reset completed successfully.");
        return;
    }

    if (migrationOnly || demoSeedOnly)
    {
        Log.Information(
            demoSeedOnly
                ? "Demo migration and seed completed successfully."
                : "Database migration completed successfully.");
        return;
    }

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (!app.Environment.IsDevelopment())
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        await next();
    });
    app.UseResponseCompression();

    if (app.Environment.IsProduction())
    {
        app.UseHsts();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Coding API v1");
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();
            options.InjectStylesheet("/swagger-ui/custom.css");
        });
    }

    // Local Vite/Nginx development proxies use the HTTP launch endpoint. Redirecting
    // proxied API calls to the HTTPS development certificate breaks browser requests.
    // Production TLS is still enforced here and by the reverse proxy/HSTS.
    if (app.Environment.IsProduction() &&
        !string.Equals(
            Environment.GetEnvironmentVariable("RENDER"),
            "true",
            StringComparison.OrdinalIgnoreCase))
    {
        app.UseHttpsRedirection();
    }
    app.UseCors("Client");
    app.UseStaticFiles();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<DemoModeGuardMiddleware>();
    app.UseAuthorization();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    }).AllowAnonymous();
    app.MapControllers();
    app.MapHub<CollaborationHub>("/hubs/collaboration");

    if (!EF.IsDesignTime)
    {
        app.Run();
    }
}
catch (HostAbortedException)
{
    // EF Core tooling intentionally aborts the temporary host after resolving services.
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
