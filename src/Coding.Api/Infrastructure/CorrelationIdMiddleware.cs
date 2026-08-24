using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace Coding.Api.Infrastructure;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValid(supplied)
            ? supplied!
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
            await next(context);
    }

    public static bool IsValid(string? value) =>
        value is { Length: >= 8 and <= 64 } && CorrelationIdPattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}
