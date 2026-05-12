using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Host.Configuration.Options;

namespace SaasBuilder.Host.RateLimiting;

/// <summary>
/// Per-tenant sliding-window rate limiter registration.
/// Partitions the rate-limit budget by tenant ID (from JWT claim or header) so that
/// a noisy tenant cannot exhaust the global rate-limit budget and impact other tenants.
/// </summary>
/// <remarks>
/// <para>
/// Config keys (all optional — defaults shown):
/// <list type="bullet">
///   <item><c>RateLimit:PerTenant:PermitLimit</c> — default 100 requests per window</item>
///   <item><c>RateLimit:PerTenant:WindowSeconds</c> — default 60</item>
///   <item><c>RateLimit:PerTenant:QueueLimit</c> — default 0</item>
///   <item><c>RateLimit:PerTenant:SoftLimitPercent</c> — default 80 (80 % = soft-limit warning)</item>
/// </list>
/// </para>
/// <para>
/// Partition key priority:
/// 1. <c>tenant_id</c> JWT claim (most authoritative).
/// 2. <c>X-Tenant-Id</c> header (service-account requests).
/// 3. Client IP address (fallback).
/// </para>
/// <para>
/// When a request exceeds 80 % of the permit limit the response includes:
/// <c>X-RateLimit-Soft-Exceeded: true</c>. Hard-limit rejections return 429 with ProblemDetails.
/// </para>
/// </remarks>
public static class PerTenantSlidingWindow
{
    /// <summary>
    /// Named rate-limit policy applied to tenant-scoped endpoints that opt in via
    /// <c>.RequireRateLimiting(PerTenantSlidingWindowPolicy)</c>.
    /// </summary>
    public const string PerTenantSlidingWindowPolicy = "per-tenant-sliding-window";

    private const string TenantIdClaimType = "tenant_id";
    private const string TenantIdHeader = "X-Tenant-Id";

    // OpenIddict passthrough routes are not registered as minimal-API endpoints, so they
    // cannot use .RequireRateLimiting() endpoint metadata. The global limiter applies a
    // fixed-window policy to all /connect/* requests before OpenIddict processes them.
    private const string ConnectPathPrefix = "/connect/";

    /// <summary>
    /// Registers the per-tenant sliding-window rate limiter.
    /// Called from <c>SaasBuilderHostExtensions</c> when
    /// <see cref="SaasBuilderRateLimitingOptions.UsePerTenantWindow"/> is true.
    /// </summary>
    public static IServiceCollection AddPerTenantSlidingWindowRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("RateLimit:PerTenant");
        int permitLimit = section.GetValue("PermitLimit", 100);
        int windowSeconds = section.GetValue("WindowSeconds", 60);
        int queueLimit = section.GetValue("QueueLimit", 0);
        int softLimitPercent = section.GetValue("SoftLimitPercent", 80);

        // Auth-endpoint fixed-window config (also used by AddSaasBuilderRateLimiting).
        // Captured by reference (IConfiguration) so overrides applied after service registration
        // (e.g., WebApplicationFactory test overrides) are respected when the limiter is first used.
        // The values are read lazily inside the GlobalLimiter factory delegate.
        IConfiguration authConfig = configuration;

        int softLimitThreshold = (int)Math.Ceiling(permitLimit * (softLimitPercent / 100.0));

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiterOptions.OnRejected = async (ctx, cancellationToken) =>
            {
                ProblemDetails problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/429",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Rate limit exceeded. Retry after the window resets.",
                };

                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.Headers.RetryAfter =
                    windowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                ctx.HttpContext.Response.ContentType = "application/problem+json";

                await ctx.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken)
                    .ConfigureAwait(false);
            };

            limiterOptions.AddSlidingWindowLimiter(PerTenantSlidingWindowPolicy, options =>
            {
                options.AutoReplenishment = true;
                options.PermitLimit = permitLimit;
                options.Window = TimeSpan.FromSeconds(windowSeconds);
                options.SegmentsPerWindow = 4;
                options.QueueLimit = queueLimit;
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            // Global limiter:
            // - /connect/* (OpenIddict token/authorize/revoke): fixed-window, tighter limit,
            //   partitioned by client_id or IP. Protects against credential-stuffing.
            // - All other paths: per-tenant sliding-window applied to authenticated requests.
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                string path = httpContext.Request.Path.Value ?? string.Empty;

                if (path.StartsWith(ConnectPathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Read auth limit lazily on first use so WebApplicationFactory test config
                    // overrides (applied after AddRateLimiter runs) are respected.
                    int lazyAuthPermit = authConfig.GetSection("RateLimit:AuthEndpoints").GetValue("PermitLimit", 10);
                    int lazyAuthWindow = authConfig.GetSection("RateLimit:AuthEndpoints").GetValue("WindowSeconds", 60);

                    // Partition by IP address — client_id cannot be read synchronously in the
                    // GlobalLimiter callback (form body is buffered lazily by downstream middleware).
                    string authPartitionKey = ResolveAuthPartitionKey(httpContext);

                    // Include PermitLimit in the partition key so test config overrides get their own
                    // partition (avoids cache poisoning from the default-limit partition).
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"auth:{lazyAuthPermit}:{authPartitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = lazyAuthPermit,
                            Window = TimeSpan.FromSeconds(lazyAuthWindow),
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        });
                }

                string partitionKey = ResolvePartitionKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });
        });

        // Register middleware to add soft-limit warning header.
        // The middleware reads the rate limit metadata after the limiter runs.
        // PerTenantRateLimitMiddleware is constructed by ASP.NET via ActivatorUtilities when
        // app.UseMiddleware<T>() runs — it does not need to be registered as a DI service, and
        // registering it would fail container validation (RequestDelegate is supplied by the
        // pipeline at runtime, not by DI).

        // Register marker options so UseSaasBuilderPipeline can check whether per-tenant
        // middleware was registered without resolving a transient instance as a presence probe
        // (resolving a transient as a probe always succeeds because DI creates a new instance).
        services.AddSingleton(new PerTenantRateLimitOptions
        {
            Enabled = true,
            SoftLimitThreshold = softLimitThreshold,
            PermitLimit = permitLimit,
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext httpContext)
    {
        // 1. Per-tenant partition: tenant_id JWT claim.
        string? tenantId = httpContext.User?.FindFirst(TenantIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return $"tenant:{tenantId}";
        }

        // 2. X-Tenant-Id header (service-account requests).
        string? headerTenantId = httpContext.Request.Headers[TenantIdHeader].ToString();
        if (!string.IsNullOrWhiteSpace(headerTenantId))
        {
            return $"tenant:{headerTenantId}";
        }

        // 3. Fallback: remote IP.
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Resolves the partition key for auth endpoints (/connect/*).
    /// Uses remote IP address — client_id from form body cannot be read synchronously in the
    /// global limiter callback (form is buffered lazily by ASP.NET Core middleware downstream).
    /// IP-based partitioning is sufficient: each IP gets its own fixed-window budget.
    /// </summary>
    private static string ResolveAuthPartitionKey(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
