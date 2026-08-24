using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Coding.Application.Features.AiAgent;

namespace Coding.Infrastructure.AiAgent;

/// <summary>
/// Detects and redacts secret-shaped substrings before they reach the model
/// or any persistent log. Implements <see cref="IAiSecretRedactionService"/>.
/// Patterns are conservative: prefer false positives over leaks.
/// </summary>
public sealed class AiSecretRedactionService : IAiSecretRedactionService
{
    private const string RedactedMarker = "[REDACTED]";

    private static readonly (Regex Pattern, string Replacement)[] RedactionPatterns =
    {
        // Authorization: Bearer / Basic headers
        (new Regex(@"Authorization:\s*(Bearer|Basic|Token)\s+[A-Za-z0-9\-_\.=]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
            $"Authorization: $1 {RedactedMarker}"),

        // JWT tokens (three base64url segments separated by dots)
        (new Regex(@"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled),
            RedactedMarker),

        // AWS access keys
        (new Regex(@"AKIA[0-9A-Z]{8,}", RegexOptions.Compiled),
            RedactedMarker),

        // AWS secret assignments
        (new Regex(@"AWS_(SECRET_)?ACCESS_KEY([_-]?ID)?\s*[=:]\s*[A-Za-z0-9/+=]{8,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
            $"AWS_ACCESS_KEY_ID={RedactedMarker}"),

        // Generic connection strings with username/password
        (new Regex(@"(?i)(password|pwd)\s*[=:]\s*[^;\s,]+", RegexOptions.Compiled),
            $"password={RedactedMarker}"),

        // PostgreSQL / MySQL connection strings
        (new Regex(@"(?i)(postgres|mysql|mongodb(\+srv)?):\/\/[^:\s]+:[^@\s]+@",
            RegexOptions.Compiled),
            $"{RedactedMarker}://{RedactedMarker}@{RedactedMarker}"),

        // PEM private key blocks
        (new Regex(@"-----BEGIN (RSA |EC |DSA |OPENSSH |)PRIVATE KEY-----[\s\S]+?-----END (RSA |EC |DSA |OPENSSH |)PRIVATE KEY-----",
            RegexOptions.Compiled),
            $"-----BEGIN PRIVATE KEY----- {RedactedMarker} -----END PRIVATE KEY-----"),

        // GitHub / GitLab personal access tokens
        (new Regex(@"\b(ghp_|gho_|ghu_|ghs_|ghr_|glpat-)[A-Za-z0-9_\-]{16,}",
            RegexOptions.Compiled),
            RedactedMarker),

        // Common API key prefixes
        (new Regex(@"\b(sk-[A-Za-z0-9_\-]{16,}|xox[abprs]-[A-Za-z0-9_\-]{8,})",
            RegexOptions.Compiled),
            RedactedMarker),

        // Generic secret-bearing configuration assignments. Require a
        // reasonably long value to avoid masking ordinary variable names.
        (new Regex("""(?i)\b(api[_-]?key|access[_-]?token|auth[_-]?token|client[_-]?secret|private[_-]?key|secret)\s*[=:]\s*['"]?[^\s;'",]{8,}""",
            RegexOptions.Compiled),
            $"$1={RedactedMarker}")
    };

    private static readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        ".env.local",
        ".env.production",
        "id_rsa",
        "id_dsa",
        "id_ecdsa",
        "id_ed25519",
        "credentials",
        "credentials.json",
        "secrets.json",
        "secrets.yaml",
        "secrets.yml",
        "appsettings.Production.json",
        ".npmrc",
        ".pypirc",
        ".netrc",
        ".dockercfg"
    };

    private static readonly string[] BlockedFileSuffixes =
    {
        ".pem", ".key", ".pfx", ".p12", ".keystore"
    };

    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var result = input;
        foreach (var (pattern, replacement) in RedactionPatterns)
            result = pattern.Replace(result, replacement);
        return result;
    }

    public bool IsSecretFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var normalized = filePath.Replace('\\', '/').TrimStart('/');
        var name = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(name)) return false;
        if (BlockedFileNames.Contains(name)) return true;
        if (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var suffix in BlockedFileSuffixes)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Computes a stable SHA-256 hash of a normalized JSON argument object
    /// so semantically equivalent arguments always produce the same key.
    /// </summary>
    public static string HashArguments(JsonElement arguments)
    {
        var canonical = Canonicalize(arguments);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>
    /// Produces a stable JSON serialization of a JSON element. Object keys
    /// are sorted alphabetically so the same logical arguments produce the
    /// same string regardless of insertion order.
    /// </summary>
    public static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            WriteCanonical(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteStringValue(element.GetRawText());
                break;
        }
    }
}
