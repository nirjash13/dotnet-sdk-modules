using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.Host.Configuration;

/// <summary>
/// Extension methods to add fixed-window rate limiting for auth endpoints.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Named rate-limit policy applied to application endpoints that opt in via
    /// <c>.RequireRateLimiting(AuthEndpointsPolicy)</c>.
    /// </summary>
    public const string AuthEndpointsPolicy = "auth-endpoints";

    // OpenIddict passthrough routes are not registered as minimal-API endpoints, so they
    // cannot use .RequireRateLimiting() endpoint metadata. The global limiter applies the
    // same fixed-window policy to all /connect/* requests regardless of route registration.
    private const string ConnectPathPrefix = "/connect/";

    /// <summary>
    /// Registers the chassis rate limiter with a fixed-window policy for auth endpoints.
    /// </summary>
    /// <remarks>
    /// Config keys (all optional — defaults shown):
    /// <list type="bullet">
    ///   <item><c>RateLimit:AuthEndpoints:PermitLimit</c> — default 10</item>
    ///   <item><c>RateLimit:AuthEndpoints:WindowSeconds</c> — default 60</item>
    ///   <item><c>RateLimit:AuthEndpoints:QueueLimit</c> — default 0</item>
    /// </list>
    /// Partition key: <c>client_id</c> from form body when present, else client IP.
    /// Rejected requests receive 429 with a <c>ProblemDetails</c> body.
    /// Global policy additionally covers <c>/connect/*</c> (OpenIddict passthrough).
    /// </remarks>
    public static IServiceCollection AddChassisRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimit:AuthEndpoints");
        int permitLimit = section.GetValue("PermitLimit", 10);
        int windowSeconds = section.GetValue("WindowSeconds", 60);
        int queueLimit = section.GetValue("QueueLimit", 0);

        services.AddRateLimiter(options =>
        {
            // 429 response with ProblemDetails body (RFC 7807)
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, cancellationToken) =>
            {
                var problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/429",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Rate limit exceeded for auth endpoint.",
                };

                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken)
                    .ConfigureAwait(false);
            };

            // Global limiter: auto-applies the same fixed-window policy to /connect/* paths
            // (OpenIddict token/authorize/revoke/introspect endpoints).
            // All other paths are unlimited at the global level; named policy handles opt-in.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                string path = httpContext.Request.Path.Value ?? string.Empty;
                if (!path.StartsWith(ConnectPathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // No-op partition — no limit for non-OIDC paths globally.
                    return RateLimitPartition.GetNoLimiter<string>("no-limit");
                }

                string partitionKey = ResolvePartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"global:{partitionKey}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });

            // Named fixed-window policy: opt-in for application endpoints via
            // .RequireRateLimiting(AuthEndpointsPolicy)
            options.AddPolicy(AuthEndpointsPolicy, httpContext =>
            {
                string partitionKey = ResolvePartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"named:{partitionKey}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext httpContext)
    {
        // Prefer client_id from form body (OAuth token requests use application/x-www-form-urlencoded).
        string? clientId = null;
        if (httpContext.Request.HasFormContentType
            && httpContext.Request.Form.TryGetValue("client_id", out var formValue))
        {
            clientId = formValue.ToString();
        }

        return !string.IsNullOrWhiteSpace(clientId)
            ? clientId
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
