namespace Coding.Infrastructure.Debugging;

public static class DebuggingCorrelationPolicy
{
    public static bool CommitTouchesPath(string patch, string path) =>
        !string.IsNullOrWhiteSpace(path) && patch.Split('\n').Any(line => line == $"+++ b/{path}" || line == $"--- a/{path}");

    public static bool SupportsRegressionClaim(DateTime? lastSuccessfulRunAt, DateTime committedAt, DateTime failedAt, bool touchesAffectedFile) =>
        touchesAffectedFile && lastSuccessfulRunAt.HasValue && lastSuccessfulRunAt.Value < committedAt && committedAt < failedAt;
}
