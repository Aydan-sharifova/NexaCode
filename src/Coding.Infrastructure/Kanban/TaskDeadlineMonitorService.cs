using Coding.Application.Features.Notifications;
using Coding.Data;
using Coding.Enums;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Kanban;

public sealed class TaskDeadlineMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<TaskDeadlineMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckDeadlinesAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckDeadlinesAsync(stoppingToken);
        }
    }

    private async Task CheckDeadlinesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var now = DateTime.UtcNow;
            var overdue = await db.ProjectTasks.AsNoTracking()
                .Where(task => task.DueDate.HasValue && task.DueDate <= now && task.Status != ProjectTaskStatus.Done)
                .Select(task => new { task.ID, task.Title, task.Project.OwnerId })
                .ToListAsync(cancellationToken);
            if (overdue.Count == 0) return;

            var taskIds = overdue.Select(task => task.ID).ToArray();
            var notifiedIds = await db.Notifications.AsNoTracking()
                .Where(notification => notification.Type == NotificationType.TaskDeadlineExceeded &&
                    notification.RelatedEntityType == nameof(ProjectTask) &&
                    notification.RelatedEntityId.HasValue && taskIds.Contains(notification.RelatedEntityId.Value))
                .Select(notification => notification.RelatedEntityId!.Value)
                .ToListAsync(cancellationToken);
            var notified = notifiedIds.ToHashSet();
            await notifications.CreateManyAsync(overdue
                .Where(task => !notified.Contains(task.ID))
                .Select(task => new CreateNotificationRequest(
                    task.OwnerId,
                    NotificationType.TaskDeadlineExceeded,
                    "Task deadline exceeded",
                    $"The deadline for '{task.Title}' has passed. The task is now locked.",
                    task.ID,
                    nameof(ProjectTask))), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not check overdue project tasks.");
        }
    }
}
