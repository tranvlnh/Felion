namespace Felion.Host.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        httpContext.Response.OnStarting(() =>
        {
            var headers = httpContext.Response.Headers;
            var contentSecurityPolicy = httpContext.Request.Path.StartsWithSegments("/admin/probation")
                ? "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'"
                : "default-src 'none'; base-uri 'none'; frame-ancestors 'none'";
            headers.TryAdd("Content-Security-Policy", contentSecurityPolicy);
            if (httpContext.Request.Path.StartsWithSegments("/admin/probation")
                && httpContext.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                headers.TryAdd("Cache-Control", "no-store");
            }

            headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            return Task.CompletedTask;
        });

        await next(httpContext);
    }
}
