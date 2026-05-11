using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SaasBuilder.Host.RateLimiting;

/// <summary>
/// Middleware that appends the <c>X-RateLimit-Soft-Exceeded: true</c> response header
/// when the per-tenant request count has exceeded the configured soft-limit threshold
/// (default 80 % of the hard limit).
/// </summary>
/// <remarks>
/// Must be registered after <c>UseRateLimiter()</c> in the middleware pipeline so that
/// the rate-limiter metadata is already attached to the response.
/// </remarks>
internal sealed class PerTenantRateLimitMiddleware
{
    private const string SoftExceededHeader = "X-RateLimit-Soft-Exceeded";

    private readonly int _softLimitThreshold;
    private readonly int _permitLimit;

    internal PerTenantRateLimitMiddleware(int softLimitThreshold, int permitLimit)
    {
        _softLimitThreshold = softLimitThreshold;
        _permitLimit = permitLimit;
    }

    /// <summary>
    /// Invokes the next middleware and, if the rate limit has been used beyond the soft threshold,
    /// appends the warning header to the response.
    /// </summary>
    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context).ConfigureAwait(false);

        // The RateLimitingMiddleware attaches lease metadata; we inspect it post-execution.
        // If the remaining permits fall below the soft threshold emit the header.
        RateLimitLease? lease = context.Features.Get<RateLimitLease>();
        if (lease is not null &&
            lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan _))
        {
            // Request was rejected (429 already set). Nothing more to do here.
            return;
        }

        // No built-in way to read current permits from IHttpRateLimiterFeature without
        // a custom policy. We emit the soft header based on the X-RateLimit-Remaining header
        // that the built-in rate limiter writes when configured (UseRateLimiter adds it by default).
        if (context.Response.Headers.TryGetValue("X-RateLimit-Remaining", out var remaining) &&
            int.TryParse(remaining.ToString(), out int remainingCount) &&
            _permitLimit - remainingCount >= _softLimitThreshold)
        {
            context.Response.Headers[SoftExceededHeader] = "true";
        }
    }
}
