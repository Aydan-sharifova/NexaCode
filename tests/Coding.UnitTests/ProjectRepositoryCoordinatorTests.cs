using Coding.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class ProjectRepositoryCoordinatorTests
{
    [Fact]
    public async Task Same_project_operations_are_serialized()
    {
        var coordinator = new ProjectRepositoryCoordinator();
        var projectId = Guid.NewGuid();
        await using var first = await coordinator.AcquireAsync(projectId);

        var secondTask = coordinator.AcquireAsync(projectId).AsTask();
        await Task.Delay(50);
        secondTask.IsCompleted.Should().BeFalse();

        await first.DisposeAsync();
        await using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Different_projects_do_not_block_each_other()
    {
        var coordinator = new ProjectRepositoryCoordinator();
        await using var first = await coordinator.AcquireAsync(Guid.NewGuid());

        await using var second = await coordinator.AcquireAsync(Guid.NewGuid()).AsTask().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Waiting_operation_observes_cancellation()
    {
        var coordinator = new ProjectRepositoryCoordinator();
        var projectId = Guid.NewGuid();
        await using var first = await coordinator.AcquireAsync(projectId);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = async () => await coordinator.AcquireAsync(projectId, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
