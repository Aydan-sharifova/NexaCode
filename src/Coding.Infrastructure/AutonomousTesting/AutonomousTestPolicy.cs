namespace Coding.Infrastructure.AutonomousTesting;

public static class AutonomousTestPolicy
{
    public const int MinimumIterations = 1;
    public const int MaximumIterations = 3;

    public static int ClampIterations(int requested) => Math.Clamp(requested, MinimumIterations, MaximumIterations);

    public static bool IsPassingExecution(int? exitCode, bool timedOut, string? standardError) =>
        exitCode is 0 && !timedOut && string.IsNullOrWhiteSpace(standardError);

    public static bool HasDedicatedRunner(string generatedHarness) =>
        generatedHarness.Contains("class AutonomousTestRunner", StringComparison.Ordinal) &&
        generatedHarness.Contains("static void Main", StringComparison.Ordinal);
}
