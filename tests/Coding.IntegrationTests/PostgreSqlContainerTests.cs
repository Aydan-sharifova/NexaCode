using FluentAssertions;
using Npgsql;
using System.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace Coding.IntegrationTests;

public sealed class PostgreSqlContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("coding_tests")
        .WithUsername("coding")
        .WithPassword("coding-tests-only")
        .Build();

    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [DockerFact]
    public async Task PostgreSQL_testcontainer_is_available_for_database_integration_tests()
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1", connection);

        var result = await command.ExecuteScalarAsync();

        result.Should().Be(1);
    }
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerIsRunning())
            Skip = "Docker is not running; start Docker Desktop to execute PostgreSQL integration tests.";
    }

    private static bool DockerIsRunning()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process is not null &&
                   process.WaitForExit(3000) &&
                   process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
