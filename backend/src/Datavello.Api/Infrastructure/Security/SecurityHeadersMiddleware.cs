namespace Datavello.Api.Infrastructure.Security;

/// <summary>
/// Defensive response headers. API responses are JSON only, so they get a locked-down CSP;
/// Swagger UI (development only) is left without CSP because it relies on inline scripts.
/// The SPA's own CSP belongs to whatever serves the frontend (see docs/architecture.md).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Cache-Control"] = "no-store";
        }

        return next(context);
    }
}
