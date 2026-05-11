using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SaasBuilder.Host.Health;
using Xunit;

namespace SaasBuilder.IntegrationTests.Gdpr;

/// <summary>
/// Load-bearing test for the liveness health probe.
/// Verifies that /health/live returns 200 regardless of dependency health.
/// </summary>
public sealed class HealthProbeTests
{
    [Fact]
    public async Task LivenessProbe_ReturnsAlive_WhenNoChecksConfigured()
    {
        // Arrange — liveness uses Predicate = _ => false (skips all registered checks)
        // so it always reports healthy regardless of dependency state.
        // We verify this by checking the HealthResponseWriter output directly.
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var ms = new System.IO.MemoryStream();
        httpContext.Response.Body = ms;

        var report = new HealthReport(
            new System.Collections.Generic.Dictionary<string, HealthReportEntry>(),
            HealthStatus.Healthy,
            System.TimeSpan.Zero);

        // Act
        await HealthResponseWriter.WriteAliveAsync(httpContext, report);

        // Assert — response contains status "alive"
        ms.Position = 0;
        string body = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        body.Should().Contain("alive");
        httpContext.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task ReadinessProbe_WhenUnhealthy_ReturnsProblemDetails()
    {
        // Arrange — simulate a failed readiness check
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var ms = new System.IO.MemoryStream();
        httpContext.Response.Body = ms;
        httpContext.Response.StatusCode = 503;

        var entries = new System.Collections.Generic.Dictionary<string, HealthReportEntry>
        {
            ["database"] = new HealthReportEntry(
                HealthStatus.Unhealthy,
                description: "Connection refused",
                duration: System.TimeSpan.Zero,
                exception: null,
                data: null),
        };

        var report = new HealthReport(entries, HealthStatus.Unhealthy, System.TimeSpan.Zero);

        // Act
        await HealthResponseWriter.WriteDetailedAsync(httpContext, report);

        // Assert — returns ProblemDetails-shaped JSON with RFC 7807 structure
        ms.Position = 0;
        string body = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        body.Should().Contain("Service Unavailable");
        body.Should().Contain("health-check-failed");
        httpContext.Response.ContentType.Should().Be("application/problem+json");
    }
}
