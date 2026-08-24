namespace Coding.Api.Configuration;

internal static class EnvironmentFile
{
    public static void LoadForDevelopment(string startDirectory)
    {
        var path = Find(startDirectory);
        if (path is not null)
        {
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
        }

        // Deployment platforms inject environment variables without a local
        // .env file. Keep their conventional names compatible with ASP.NET
        // Core's hierarchical configuration keys in every environment.
        MapAlias("JWT_ISSUER", "Jwt__Issuer");
        MapAlias("JWT_AUDIENCE", "Jwt__Audience");
        MapAlias("JWT_KEY", "Jwt__Key");
        // ASP.NET Core treats environment keys case-insensitively, while the
        // operating system does not. A stale JWT__KEY can otherwise compete
        // with the canonical JWT_KEY and make signing-key selection depend on
        // environment enumeration order. Keep the equivalent form aligned.
        AlignEquivalentAlias("JWT_KEY", "JWT__KEY");
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
        MapAlias("AI_PROVIDER", "AI__Provider");
        MapAlias("OPENAI_COMPATIBLE_BASE_URL", "OpenAICompatible__BaseUrl");
        MapAlias("OPENAI_COMPATIBLE_MODEL", "OpenAICompatible__Model");
        MapAlias("OPENAI_COMPATIBLE_VISION_MODEL", "OpenAICompatible__VisionModel");
        MapAlias("OPENAI_COMPATIBLE_API_KEY", "OpenAICompatible__ApiKey");
        MapAlias("OPENAI_COMPATIBLE_MAX_OUTPUT_TOKENS", "OpenAICompatible__MaxOutputTokens");
        MapAlias("OPENAI_COMPATIBLE_TEMPERATURE", "OpenAICompatible__Temperature");
        MapAlias("EXECUTION_ENABLED", "Execution__Enabled");
        MapAlias("EXECUTION_DOTNET_IMAGE", "Execution__DotNetImage");
    }

    private static void MapAlias(string source, string destination)
    {
        if (Environment.GetEnvironmentVariable(destination) is not null) return;

        var value = Environment.GetEnvironmentVariable(source);
        if (!string.IsNullOrWhiteSpace(value))
            Environment.SetEnvironmentVariable(destination, value);
    }

    private static void AlignEquivalentAlias(string source, string equivalent)
    {
        var value = Environment.GetEnvironmentVariable(source);
        if (!string.IsNullOrWhiteSpace(value))
            Environment.SetEnvironmentVariable(equivalent, value);
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
