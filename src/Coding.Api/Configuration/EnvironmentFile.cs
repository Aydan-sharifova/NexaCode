namespace Coding.Api.Configuration;

internal static class EnvironmentFile
{
    public static void LoadForDevelopment(string startDirectory)
    {
        var path = Find(startDirectory);
        if (path is null) return;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            var value = Unquote(line[(separator + 1)..].Trim());
            if (key.Length == 0 ||
                Environment.GetEnvironmentVariable(key) is not null)
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }

        MapAlias("JWT_ISSUER", "Jwt__Issuer");
        MapAlias("JWT_AUDIENCE", "Jwt__Audience");
        MapAlias("JWT_KEY", "Jwt__Key");
        MapAlias("SMTP_ENABLED", "Smtp__Enabled");
        MapAlias("SMTP_HOST", "Smtp__Host");
        MapAlias("SMTP_PORT", "Smtp__Port");
        MapAlias("SMTP_USE_SSL", "Smtp__UseSsl");
        MapAlias("SMTP_USE_STARTTLS", "Smtp__UseStartTls");
        MapAlias("SMTP_CHECK_CERTIFICATE_REVOCATION", "Smtp__CheckCertificateRevocation");
        MapAlias("SMTP_USERNAME", "Smtp__Username");
        MapAlias("SMTP_PASSWORD", "Smtp__Password");
        MapAlias("SMTP_FROM_EMAIL", "Smtp__FromEmail");
        MapAlias("SMTP_FROM_NAME", "Smtp__FromName");
        MapAlias("FRONTEND_ORIGIN", "Smtp__ClientBaseUrl");
    }

    private static void MapAlias(string source, string destination)
    {
        if (Environment.GetEnvironmentVariable(destination) is not null) return;

        var value = Environment.GetEnvironmentVariable(source);
        if (!string.IsNullOrWhiteSpace(value))
            Environment.SetEnvironmentVariable(destination, value);
    }

    private static string? Find(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];

        return value;
    }
}
