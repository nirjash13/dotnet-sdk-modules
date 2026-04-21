using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Chassis.Host.Observability;

/// <summary>
/// Wires OpenTelemetry tracing and metrics into the DI container, exporting via OTLP gRPC.
/// Serilog log forwarding to the OTel Collector is configured separately in
/// <c>ChassisHostExtensions.AddChassisHost</c> via <c>builder.Host.UseSerilog()</c>.
/// </summary>
internal static class OpenTelemetrySetup
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics services.
    /// </summary>
    /// <remarks>
    /// OTel endpoint resolution order (first non-empty wins):
    /// <list type="number">
    ///   <item>Environment variable <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (handled natively by the SDK).</item>
    ///   <item>Configuration key <c>Otel:Endpoint</c>.</item>
    ///   <item>Default <c>http://localhost:4317</c> (dev-only fallback — no secrets in code).</item>
    /// </list>
    /// </remarks>
    internal static IServiceCollection AddChassisObservability(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        string otlpEndpoint = config["Otel:Endpoint"] ?? "http://localhost:4317";

        // Service version from assembly metadata; falls back to "0.0.0" during local dev before
        // MinVer tags are applied.
        string serviceVersion = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString()
            ?? "0.0.0";

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: "chassis-host",
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Skip health and OpenAPI probes to reduce trace noise.
                    options.Filter = ctx => ctx.Request.Path.Value is not "/health"
                        && ctx.Request.Path.Value is not "/openapi"
                        && ctx.Request.Path.Value is not "/scalar";
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    // Never capture DB statement text outside Development — statements may contain PII.
                    options.SetDbStatementForText = env.IsDevelopment();
                })
                .AddSource("Chassis.Host")
                .AddSource("MassTransit")
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otlpEndpoint);
                    otlp.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Chassis.Host")
                .AddMeter("MassTransit")

                // TODO Phase 8: add .AddRuntimeInstrumentation() once
                // OpenTelemetry.Instrumentation.Runtime is pinned in Directory.Packages.props.
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otlpEndpoint);
                    otlp.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }
}
