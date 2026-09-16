namespace Felion.Host.Middleware;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    internal const string HeaderName = "X-Correlation-Id";
    internal const string ItemKey = "Felion.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        });

        await next(context);
    }

    private static string GetCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && Guid.TryParse(headerValue.FirstOrDefault(), out var suppliedId))
        {
            return suppliedId.ToString("N");
        }

        return Guid.NewGuid().ToString("N");
    }
}
