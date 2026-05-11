using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SaasBuilder.Host.Health;

/// <summary>JSON response writers for health check endpoints.</summary>
internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Writes a minimal alive response — always status "alive".</summary>
    public static Task WriteAliveAsync(HttpContext context, HealthReport report)
    {
        // report is intentionally unused — liveness never inspects checks.
        _ = report;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = "alive" }, JsonOptions));
    }

    /// <summary>
    /// Writes a detailed health response. Returns RFC 7807 ProblemDetails
    /// on unhealthy status so clients receive a consistent error shape.
    /// </summary>
    public static async Task WriteDetailedAsync(HttpContext context, HealthReport report)
    {
        if (report.Status == HealthStatus.Unhealthy)
        {
            context.Response.ContentType = "application/problem+json";
            var problem = new
            {
                type = "https://saasbuilder.dev/problems/health-check-failed",
                title = "Service Unavailable",
                status = StatusCodes.Status503ServiceUnavailable,
                detail = "One or more health checks failed.",
                errors = BuildErrors(report),
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions))
                .ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = BuildErrors(report),
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions))
            .ConfigureAwait(false);
    }

    private static object BuildErrors(HealthReport report)
    {
        var dict = new Dictionary<string, string>();
        foreach (KeyValuePair<string, HealthReportEntry> entry in report.Entries)
        {
            dict[entry.Key] = entry.Value.Status.ToString();
        }

        return dict;
    }
}
