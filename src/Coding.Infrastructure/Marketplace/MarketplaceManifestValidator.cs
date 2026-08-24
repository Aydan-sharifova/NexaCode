using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Coding.Application.Features.AiAgent;
using Coding.Application.Features.Marketplace;
using Coding.Enums;
using Coding.Exceptions;

namespace Coding.Infrastructure.Marketplace;

public sealed partial class MarketplaceManifestValidator(IAiToolRegistry tools) : IMarketplaceManifestValidator
{
    private const int MaximumManifestBytes = 64 * 1024;

    public MarketplaceValidatedManifest Validate(MarketplaceCategory category, JsonElement manifest, IReadOnlyList<string> permissions)
    {
        if (manifest.ValueKind != JsonValueKind.Object) throw new ConflictException("Marketplace manifest must be a JSON object.");
        var manifestJson = JsonSerializer.Serialize(manifest);
        if (Encoding.UTF8.GetByteCount(manifestJson) > MaximumManifestBytes) throw new ConflictException("Marketplace manifest exceeds 64 KB.");
        var normalizedPermissions = permissions.Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Order().ToArray();
        var unknown = normalizedPermissions.Where(permission => !MarketplacePermissions.Allowed.Contains(permission)).ToArray();
        if (unknown.Length > 0) throw new ConflictException("Unknown marketplace permission(s): " + string.Join(", ", unknown));

        switch (category)
        {
            case MarketplaceCategory.AiAgent: ValidateAgent(manifest); break;
            case MarketplaceCategory.Plugin: ValidatePlugin(manifest); break;
            case MarketplaceCategory.Theme: ValidateTheme(manifest); break;
            case MarketplaceCategory.ProjectTemplate: ValidateFiles(manifest, true); break;
            case MarketplaceCategory.Component: ValidateFiles(manifest, false); break;
            case MarketplaceCategory.Snippet: ValidateSnippet(manifest); break;
            default: throw new ConflictException("Unsupported marketplace category.");
        }

        var permissionsJson = JsonSerializer.Serialize(normalizedPermissions);
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson + "\n" + permissionsJson)));
        return new(manifestJson, permissionsJson, normalizedPermissions, checksum);
    }

    private void ValidateAgent(JsonElement manifest)
    {
        RequiredString(manifest, "name", 120);
        RequiredString(manifest, "systemPrompt", 8000);
        if (!manifest.TryGetProperty("allowedTools", out var allowedTools) || allowedTools.ValueKind != JsonValueKind.Array)
            throw new ConflictException("AI agent manifest requires an allowedTools array.");
        foreach (var tool in allowedTools.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(tool.GetString())) throw new ConflictException("AI agent tool names must be strings.");
            try { tools.Describe(tool.GetString()!); }
            catch (Exception exception) when (exception.GetType().Name == "UnknownAiToolException") { throw new ConflictException($"AI agent requests unknown tool '{tool.GetString()}'."); }
        }
    }

    private static void ValidatePlugin(JsonElement manifest)
    {
        RequiredString(manifest, "name", 120);
        if (!manifest.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Array)
            throw new ConflictException("Plugin manifest requires a capabilities array.");
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "script", "javascript", "bundle", "entrypoint", "sourceCode", "html" };
        if (manifest.EnumerateObject().Any(property => forbidden.Contains(property.Name)))
            throw new ConflictException("Untrusted plugin manifests cannot contain executable frontend code.");
    }

    private static void ValidateTheme(JsonElement manifest)
    {
        RequiredString(manifest, "name", 120);
        if (!manifest.TryGetProperty("colors", out var colors) || colors.ValueKind != JsonValueKind.Object) throw new ConflictException("Theme manifest requires a colors object.");
        var entries = colors.EnumerateObject().ToArray();
        if (entries.Length is < 1 or > 150) throw new ConflictException("Theme must define between 1 and 150 colors.");
        foreach (var color in entries)
        {
            var value = color.Value.ValueKind == JsonValueKind.String ? color.Value.GetString()! : string.Empty;
            if (!SafeColor().IsMatch(value)) throw new ConflictException($"Theme color '{color.Name}' is not a safe CSS color value.");
        }
    }

    private static void ValidateFiles(JsonElement manifest, bool rejectSecrets)
    {
        RequiredString(manifest, "name", 120);
        if (!manifest.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Object) throw new ConflictException("Manifest requires a files object.");
        var entries = files.EnumerateObject().ToArray();
        if (entries.Length is < 1 or > 200) throw new ConflictException("Marketplace packages support between 1 and 200 files.");
        var total = 0;
        foreach (var file in entries)
        {
            ValidatePath(file.Name);
            if (file.Value.ValueKind != JsonValueKind.String) throw new ConflictException($"File '{file.Name}' content must be text.");
            var content = file.Value.GetString() ?? string.Empty;
            total += Encoding.UTF8.GetByteCount(content);
            if (total > 1024 * 1024) throw new ConflictException("Marketplace package content exceeds 1 MB.");
            if (rejectSecrets && (SecretPath().IsMatch(file.Name) || SecretContent().IsMatch(content)))
                throw new ConflictException($"Template file '{file.Name}' appears to contain a secret and cannot be published.");
        }
    }

    private static void ValidateSnippet(JsonElement manifest)
    {
        RequiredString(manifest, "language", 50);
        RequiredString(manifest, "content", 50_000);
    }

    private static void RequiredString(JsonElement manifest, string propertyName, int maximumLength)
    {
        if (!manifest.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > maximumLength)
            throw new ConflictException($"Manifest property '{propertyName}' is required and must contain at most {maximumLength} characters.");
    }

    private static void ValidatePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is "" or "." or ".." or ".git"))
            throw new ConflictException($"Marketplace file path '{path}' is unsafe.");
    }

    [GeneratedRegex("^(#[0-9a-fA-F]{3,8}|rgba?\\([0-9., %]+\\)|hsla?\\([0-9., %]+\\)|[a-zA-Z]{3,24})$")]
    private static partial Regex SafeColor();
    [GeneratedRegex("(^|/)(\\.env($|\\.)|id_rsa|id_ed25519|.*\\.(pem|p12|pfx|key)$)", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPath();
    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----|(api[_-]?key|secret|password|token)\\s*[:=]\\s*['\"]?[A-Za-z0-9_\\-]{12,}", RegexOptions.IgnoreCase)]
    private static partial Regex SecretContent();
}
