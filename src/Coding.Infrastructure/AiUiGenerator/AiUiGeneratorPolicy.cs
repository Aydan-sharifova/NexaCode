namespace Coding.Infrastructure.AiUiGenerator;

public static class AiUiGeneratorPolicy
{
    public static readonly IReadOnlyDictionary<string,string> Sections = new Dictionary<string,string>
    {
        ["src/App.tsx"] = "APP_TSX",
        ["src/pages/DashboardPage.tsx"] = "DASHBOARD_PAGE_TSX",
        ["src/components/DashboardShell.tsx"] = "DASHBOARD_SHELL_TSX",
        ["src/styles.css"] = "STYLES_CSS"
    };
    public static void ValidateFiles(IReadOnlyDictionary<string,string> files)
    {
        if (!Sections.Keys.SequenceEqual(files.Keys)) throw new InvalidOperationException("The UI generator returned an unexpected file set.");
        foreach (var (path, value) in files)
            if (value.Length is < 40 or > 120_000) throw new InvalidOperationException($"Generated {path} is outside the bounded source limits.");
        if (!files["src/App.tsx"].Contains("Route", StringComparison.Ordinal) && !files["src/App.tsx"].Contains("DashboardPage", StringComparison.Ordinal))
            throw new InvalidOperationException("Generated App.tsx does not expose page routing/composition.");
        if (!files["src/pages/DashboardPage.tsx"].Contains("export", StringComparison.Ordinal) || !files["src/components/DashboardShell.tsx"].Contains("export", StringComparison.Ordinal))
            throw new InvalidOperationException("Generated page/component modules are incomplete.");
    }

    public static void ValidateSampleDataBoundary(IEnumerable<string> sources, bool includeSampleData)
    {
        if (includeSampleData) return;
        var joined = string.Join('\n', sources);
        var forbidden = new[] { "mockData", "sampleData", "dummyData", "fakeData", "faker.", "const users = [", "const transactions = [", "const metrics = [" };
        if (forbidden.Any(token => joined.Contains(token, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Generated UI included sample records without explicit approval.");
    }
}
