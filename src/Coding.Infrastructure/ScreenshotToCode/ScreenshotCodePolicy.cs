namespace Coding.Infrastructure.ScreenshotToCode;

public static class ScreenshotCodePolicy
{
    public static string ExtractSection(string raw, string name)
    {
        var marker = $"[[[{name}]]]";
        var start = raw.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException($"Ollama vision output omitted {marker}.");
        start += marker.Length;
        var next = raw.IndexOf("[[[", start, StringComparison.Ordinal);
        return (next < 0 ? raw[start..] : raw[start..next]).Trim();
    }

    public static void ValidateGenerated(string app, string css, string preview)
    {
        if (app.Length is < 80 or > 120_000 || !app.Contains("export", StringComparison.Ordinal) || !app.Contains("return", StringComparison.Ordinal))
            throw new InvalidOperationException("Ollama did not return a valid bounded React TypeScript component.");
        if (css.Length is < 20 or > 120_000)
            throw new InvalidOperationException("Ollama did not return valid bounded CSS.");
        if (preview.Length is < 80 or > 180_000 || !preview.Contains("<html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ollama did not return a valid standalone preview.");
        if (preview.Contains("http://", StringComparison.OrdinalIgnoreCase) || preview.Contains("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generated preview attempted to load a remote resource.");
    }

    public static string SecurePreview(string preview)
    {
        const string policy = "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data: blob:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; font-src data:; connect-src 'none'; form-action 'none'; base-uri 'none'\">";
        var head = preview.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        if (head < 0) return policy + preview;
        var close = preview.IndexOf('>', head);
        return close < 0 ? policy + preview : preview.Insert(close + 1, policy);
    }
}
