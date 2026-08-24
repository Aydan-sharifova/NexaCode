namespace Coding.Application.Features.Runtime;

public sealed record RuntimeExecutionRequest(
    string Language,
    string Source,
    int TimeoutSeconds = 8,
    string? StartupObject = null);

public sealed record RuntimeExecutionResult(
    int? ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    int DurationMs);

public interface IRuntimeProvider
{
    string Name { get; }
    IReadOnlySet<string> SupportedLanguages { get; }
    Task<RuntimeExecutionResult> ExecuteAsync(
        RuntimeExecutionRequest request,
        CancellationToken cancellationToken);
}
