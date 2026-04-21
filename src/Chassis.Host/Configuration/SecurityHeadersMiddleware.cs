using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Chassis.Host.Configuration;

/// <summary>
/// Middleware that sets OWASP-recommended HTTP security headers on every response.
/// </summary>
/// <remarks>
/// HSTS (<c>Strict-Transport-Security</c>) is intentionally omitted here — it is
/// already emitted by <c>app.UseHsts()</c> in <c>UseChassisPipeline</c>.
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
{
    // ── Header value constants ────────────────────────────────────────────────
    private const string XContentTypeOptionsValue = "nosniff";
    private const string ReferrerPolicyValue = "strict-origin-when-cross-origin";
    private const string XFrameOptionsValue = "DENY";
    private const string PermissionsPolicyValue = "camera=(), microphone=(), geolocation=()";

    /// <summary>
    /// CSP for Development: permissive enough for Scalar API reference UI, which loads
    /// assets from cdn.jsdelivr.net.
    /// </summary>
    private const string CspDevelopment =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "font-src 'self' https://cdn.jsdelivr.net;";

    /// <summary>
    /// CSP for Production: strict policy; no external resources, no inline scripts.
    /// </summary>
    private const string CspProduction =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "font-src 'self'; " +
        "frame-ancestors 'none';";

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Register headers via OnStarting so they are injected before the first byte
        // is written to the response stream, even if the downstream writes the status
        // line early (e.g. minimal API Results).
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = XContentTypeOptionsValue;
            headers["Referrer-Policy"] = ReferrerPolicyValue;
            headers["X-Frame-Options"] = XFrameOptionsValue;
            headers["Permissions-Policy"] = PermissionsPolicyValue;
            headers["Content-Security-Policy"] = env.IsDevelopment()
                ? CspDevelopment
                : CspProduction;

            return Task.CompletedTask;
        });

        await next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Extension method to register <see cref="SecurityHeadersMiddleware"/> in the pipeline.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds OWASP security headers middleware to the request pipeline.
    /// Call this after <c>UseRouting()</c> and before <c>UseAuthentication()</c>.
    /// </summary>
    public static IApplicationBuilder UseChassisSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
