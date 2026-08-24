namespace Coding.Infrastructure.Caching;

public static class RedisConnectionString
{
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (!(trimmed.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase)) ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("redis" or "rediss"))
            return trimmed;

        var port = uri.IsDefaultPort ? 6379 : uri.Port;
        var options = new List<string> { $"{uri.Host}:{port}" };
        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            var password = separator >= 0 ? uri.UserInfo[(separator + 1)..] : uri.UserInfo;
            if (!string.IsNullOrWhiteSpace(password))
                options.Add($"password={Uri.UnescapeDataString(password)}");
        }
        if (uri.Scheme == "rediss") options.Add("ssl=true");
        options.Add("abortConnect=false");
        return string.Join(',', options);
    }
}
