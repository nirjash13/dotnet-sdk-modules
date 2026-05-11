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

            // Global limiter: per-tenant sliding window applied to ALL authenticated requests.
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
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
        services.AddTransient<PerTenantRateLimitMiddleware>(sp =>
            new PerTenantRateLimitMiddleware(softLimitThreshold, permitLimit));

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
}
