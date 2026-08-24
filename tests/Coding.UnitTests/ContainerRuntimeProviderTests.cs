using Coding.Application.Features.Runtime;
using Coding.Infrastructure.Runtime;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Coding.UnitTests;

public sealed class ContainerRuntimeProviderTests
{
    [Fact]
    public async Task Rejects_execution_when_deployment_has_not_enabled_it()
    {
        var provider = new ContainerRuntimeProvider(Options.Create(new ContainerRuntimeOptions()), NullLogger<ContainerRuntimeProvider>.Instance);

        var action = () => provider.ExecuteAsync(
            new RuntimeExecutionRequest("csharp", "Console.WriteLine(1);", 5),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*disabled*");
    }

    [Fact]
    public async Task Rejects_unsupported_languages_before_starting_a_process()
    {
        var provider = new ContainerRuntimeProvider(Options.Create(new ContainerRuntimeOptions
        {
            Enabled = true
        }), NullLogger<ContainerRuntimeProvider>.Instance);

        var action = () => provider.ExecuteAsync(
            new RuntimeExecutionRequest("python", "print(1)", 5),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*python*");
    }
}
