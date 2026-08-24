using Coding.Application.Features.Achievements;
using Coding.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Achievements;

public sealed class AchievementBackfillService(IServiceScopeFactory scopes, ILogger<AchievementBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var evaluator = scope.ServiceProvider.GetRequiredService<IAchievementEvaluator>();
                var userIds = await db.Users.AsNoTracking().Where(x => !x.IsDeleted && !x.IsSuspended).OrderBy(x => x.ID).Select(x => x.ID).Take(10_000).ToListAsync(stoppingToken);
                foreach (var userId in userIds) await evaluator.EvaluateAsync(userId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Achievement eligibility backfill failed; the next scheduled run will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
