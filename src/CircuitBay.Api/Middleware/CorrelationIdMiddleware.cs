namespace CircuitBay.Api.Middleware;

// Injects a correlation ID into every request for log tracing.
// If the caller provides X-Correlation-Id we echo it back; otherwise we generate one.
// The ID is stored in HttpContext.Items so downstream code/logs can reference it.
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N")[..16];

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }

    public static string? GetCurrentId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var id) ? id?.ToString() : null;
}
