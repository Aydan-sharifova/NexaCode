using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Coding.Application.Features.Collaboration;
using Coding.Data;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Collaboration;

public sealed class CollaborativeContentMaterializer(
    IServiceScopeFactory scopeFactory,
    ILogger<CollaborativeContentMaterializer> logger) : BackgroundService, ICollaborativeContentMaterializer
{
    private sealed record Work(Guid ProjectId, Guid FileId, Guid UserId, string Content, DateTime DueAt);
    private readonly Channel<Guid> queue = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, Work> pending = new();
    public void Enqueue(Guid projectId, Guid fileId, Guid userId, string content) { pending[fileId] = new(projectId, fileId, userId, content, DateTime.UtcNow.AddMilliseconds(750)); queue.Writer.TryWrite(fileId); }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var fileId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!pending.TryGetValue(fileId, out var work)) continue;
            var delay = work.DueAt - DateTime.UtcNow; if (delay > TimeSpan.Zero) await Task.Delay(delay, stoppingToken);
            if (!pending.TryGetValue(fileId, out var latest) || latest != work || !pending.TryRemove(fileId, out _)) continue;
            try
            {
                await Materialize(latest, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to materialize collaborative content for project {ProjectId}, file {FileId}.",
                    latest.ProjectId,
                    latest.FileId);
            }
        }
    }

    private async Task Materialize(Work work, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(work.Content))).ToLowerInvariant();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var state = await db.FileContents
                .SingleOrDefaultAsync(item => item.NodeId == work.FileId, ct);
            if (state is null || state.ContentHash == hash)
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            var now = DateTime.UtcNow;
            state.Content = work.Content;
            state.ContentHash = hash;
            state.ConcurrencyToken = Guid.NewGuid().ToString("N");
            state.VersionNumber++;
            state.UpdatedAt = now;
            state.UpdatedById = work.UserId;
            db.FileVersions.Add(new FileVersion
            {
                ID = Guid.NewGuid(),
                NodeId = work.FileId,
                VersionNumber = state.VersionNumber,
                Content = work.Content,
                ContentHash = hash,
                CreatedById = work.UserId,
                CreatAt = now
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
