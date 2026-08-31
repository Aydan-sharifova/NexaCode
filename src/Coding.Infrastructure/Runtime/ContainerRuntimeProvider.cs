using System.ComponentModel;
using System.Diagnostics;
using Coding.Application.Features.Runtime;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.Runtime;

public sealed class ContainerRuntimeOptions
{
    public const string SectionName = "Execution";
    public bool Enabled { get; init; }
    public string DotNetImage { get; init; } = "mcr.microsoft.com/dotnet/sdk:8.0";
    public int MaximumOutputCharacters { get; init; } = 32_000;
}

public sealed class ContainerRuntimeProvider(
    IOptions<ContainerRuntimeOptions> options,
    ILogger<ContainerRuntimeProvider> logger) : IRuntimeProvider
{
    private readonly ContainerRuntimeOptions _options = options.Value;
    public string Name => "Docker";
    public IReadOnlySet<string> SupportedLanguages { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "csharp" };

    public async Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Code execution is disabled for this deployment.");
        if (!SupportedLanguages.Contains(request.Language))
            throw new ArgumentException($"The runtime does not support '{request.Language}'.");

        var directory = Path.Combine(Path.GetTempPath(), "nexacode-runtime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), request.Source, cancellationToken);
            var startupObject = string.IsNullOrWhiteSpace(request.StartupObject) ? string.Empty :
                System.Text.RegularExpressions.Regex.IsMatch(request.StartupObject, "^[A-Za-z_][A-Za-z0-9_.]{0,199}$")
                    ? $"<StartupObject>{request.StartupObject}</StartupObject>"
                    : throw new ArgumentException("The runtime startup object is invalid.");
            await File.WriteAllTextAsync(Path.Combine(directory, "Runner.csproj"),
                $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>{startupObject}</PropertyGroup></Project>",
                cancellationToken);

            var start = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in new[]
            {
                "run", "--rm", "--network", "none", "--memory", "256m", "--cpus", "0.75",
                "--pids-limit", "64", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
                "--read-only", "--tmpfs", "/tmp:rw,noexec,nosuid,size=64m",
                "--mount", $"type=bind,source={directory},target=/workspace",
                "--workdir", "/workspace", _options.DotNetImage,
                "dotnet", "run", "--project", "Runner.csproj", "--nologo"
            }) start.ArgumentList.Add(argument);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 15)));
            var stopwatch = Stopwatch.StartNew();
            Process process;
            try
            {
                process = Process.Start(start) ?? throw new InvalidOperationException("The isolated runtime could not be started.");
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException("Docker is required for isolated code execution.", exception);
            }

            using (process)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        try { process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) { }
                    }
                    if (cancellationToken.IsCancellationRequested)
                        throw;
                    logger.LogWarning(
                        "Runtime execution timed out for language {Language} after {ElapsedMs} ms.",
                        request.Language, stopwatch.ElapsedMilliseconds);
                    return new(null, "", "Execution stopped after the time limit.", true, (int)stopwatch.ElapsedMilliseconds);
                }

                var stdout = Limit(await stdoutTask);
                var stderr = Limit(await stderrTask);
                if (process.ExitCode == 125 && stderr.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Start Docker before running server-side code.");
                logger.LogInformation(
                    "Runtime execution completed for language {Language} with exit code {ExitCode} in {ElapsedMs} ms; output was {OutputCharacters} characters.",
                    request.Language, process.ExitCode, stopwatch.ElapsedMilliseconds, stdout.Length + stderr.Length);
                return new(process.ExitCode, stdout, stderr, false, (int)stopwatch.ElapsedMilliseconds);
            }
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private string Limit(string value) => value.Length <= _options.MaximumOutputCharacters
        ? value
        : value[.._options.MaximumOutputCharacters] + "\n… output truncated";
}
