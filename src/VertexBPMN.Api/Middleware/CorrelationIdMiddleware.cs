using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VertexBPMN.Api.Middleware;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
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
        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.AddBaggage("correlation.id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await next(context);
    }

    private static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && CorrelationIdPattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}
