namespace Coding.Application.Security;
public static class RequestOriginPolicy
{
    public static bool IsAllowed(string? origin,string requestScheme,string requestHost,IEnumerable<string> configuredOrigins)
    {
        if(string.IsNullOrWhiteSpace(origin))return true;
        if(!Uri.TryCreate(origin,UriKind.Absolute,out var candidate)||candidate.Scheme is not ("http" or "https"))return false;
        var normalized=$"{candidate.Scheme}://{candidate.Authority}";
        if(string.Equals(normalized,$"{requestScheme}://{requestHost}",StringComparison.OrdinalIgnoreCase))return true;
        return configuredOrigins.Any(value=>Uri.TryCreate(value,UriKind.Absolute,out var allowed)&&string.Equals(normalized,$"{allowed.Scheme}://{allowed.Authority}",StringComparison.OrdinalIgnoreCase));
    }
}
