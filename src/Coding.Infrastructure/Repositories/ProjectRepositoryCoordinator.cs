using System.Collections.Concurrent;
using Coding.Application.Features.Repositories;

namespace Coding.Infrastructure.Repositories;

public sealed class ProjectRepositoryCoordinator : IProjectRepositoryCoordinator
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public async ValueTask<IAsyncDisposable> AcquireAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Project ID is required.", nameof(projectId));
        var gate = locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int released;
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
