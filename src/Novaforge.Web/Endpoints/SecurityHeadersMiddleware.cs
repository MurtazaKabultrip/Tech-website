namespace Novaforge.Web.Endpoints;

/// <summary>Strict browser security headers. No inline scripts are used anywhere, so script-src is 'self' only.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " + // Blazor sets inline style attributes for some components
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' wss: ws:; " + // Blazor Server circuit (WebSocket)
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            if (!context.Request.Path.StartsWithSegments("/openapi"))
            {
                headers["Content-Security-Policy"] = Csp;
            }
            return Task.CompletedTask;
        });
        return next(context);
    }
}
