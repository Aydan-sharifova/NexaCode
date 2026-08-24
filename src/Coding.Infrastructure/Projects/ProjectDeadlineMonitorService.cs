using Coding.Application.Features.Activities;
using Coding.Application.Features.Notifications;
using Coding.Data;
using Coding.Domain.Services;
using Coding.Enums;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Projects;

public sealed class ProjectDeadlineMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProjectDeadlineMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RefreshAsync(stoppingToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var activity = scope.ServiceProvider.GetRequiredService<IActivityLogger>();
            var now = DateTime.UtcNow;
            var projects = await db.Projects.Include(project => project.Members)
                .Where(project => project.DeadlineAt.HasValue &&
                    (project.Status == ProjectStatus.Active || project.Status == ProjectStatus.DeadlineSoon || project.Status == ProjectStatus.DeadlineExpired))
                .ToListAsync(cancellationToken);

            foreach (var project in projects)
            {
                var previous = project.Status;
                var effective = ProjectLifecycle.EffectiveStatus(previous, project.DeadlineAt, now);
                if (effective == previous) continue;
                project.Status = effective;
                project.UpdateAt = now;
                await db.SaveChangesAsync(cancellationToken);

                await activity.LogAsync(new(null, project.ID, "ProjectStatusChanged", nameof(Project), project.ID,
                    $"Project status changed from {previous} to {effective}.", new Dictionary<string, object?>
                    {
                        ["from"] = previous.ToString(),
                        ["to"] = effective.ToString(),
                        ["deadlineAt"] = project.DeadlineAt
                    }), cancellationToken);

                if (effective is not (ProjectStatus.DeadlineSoon or ProjectStatus.DeadlineExpired)) continue;
                var type = effective == ProjectStatus.DeadlineExpired
                    ? NotificationType.ProjectDeadlineExpired
                    : NotificationType.ProjectDeadlineSoon;
                var title = effective == ProjectStatus.DeadlineExpired
                    ? "Project deadline expired"
                    : "Project deadline approaching";
                var message = effective == ProjectStatus.DeadlineExpired
                    ? $"The deadline for '{project.Name}' has expired. Developer access is now read-only."
                    : $"The deadline for '{project.Name}' is approaching ({project.DeadlineAt:u}).";
                await notifications.CreateManyAsync(project.Members.Select(member =>
                    new CreateNotificationRequest(member.UserId, type, title, message, project.ID, nameof(Project))), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not refresh project deadline states.");
        }
    }
}
