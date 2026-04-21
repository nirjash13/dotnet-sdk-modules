using System.Collections.Generic;
using Chassis.Host.ErrorHandling;
using Chassis.Host.Modules;
using Chassis.Host.Observability;
using Chassis.Host.Tenancy;
using Chassis.Host.Transport;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Chassis.Host.Configuration;

/// <summary>
/// Extension methods that compose the full Chassis host from its constituent parts.
/// </summary>
public static class ChassisHostExtensions
{
    /// <summary>
    /// Registers all chassis services: module loader, MassTransit Mediator,
    /// pipeline filters, tenant accessor, ProblemDetails, OpenAPI, and health checks.
    /// </summary>
    /// <remarks>
    /// JWT bearer validation is wired via <see cref="AddChassisAuthenticationExtensions.AddChassisAuthentication"/>;
    /// tokens are issued by the Identity module (OpenIddict).
    /// </remarks>
    public static WebApplicationBuilder AddChassisHost(
        this WebApplicationBuilder builder)
    {
        IServiceCollection services = builder.Services;
        IConfiguration config = builder.Configuration;

        // ── Serilog — configure before any other service that might log ────────────
        // UseSerilog() replaces the default Microsoft.Extensions.Logging pipeline with
        // Serilog. The logger is also forwarded to the OTel Collector via OTLP.
        // Shutdown/flush is handled automatically by UseSerilog on host stop (no need
        // for a manual Log.CloseAndFlush() call in Program.cs — UseSerilog registers
        // a host lifetime hook that flushes before the process exits).
        string otlpEndpoint = config["Otel:Endpoint"] ?? "http://localhost:4317";

        // Register TenantLogEnricher in DI so it can resolve ITenantContextAccessor.
        // Serilog picks it up via .Enrich.With<TenantLogEnricher>() below.
        services.AddSingleton<TenantLogEnricher>();

        builder.Host.UseSerilog((hostContext, serviceProvider, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(hostContext.Configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.FromLogContext()
                .Enrich.With(serviceProvider.GetRequiredService<TenantLogEnricher>())
                .WriteTo.Console()
                .WriteTo.OpenTelemetry(otel =>
                {
                    otel.Endpoint = otlpEndpoint;
                    otel.Protocol = OtlpProtocol.Grpc;
                });
        });

        // ── ProblemDetails (RFC 7807) ──────────────────────────────────────────────
        services.AddProblemDetails();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        // ── Tenant context accessor — singleton, shared across all scoped services ─
        // NOTE: If AddChassisPersistence<T> is called by a module, it will also call
        // AddSingleton<ITenantContextAccessor, TenantContextAccessor>. AddSingleton is
        // idempotent when the same concrete type is registered — the DI container
        // honours the first registration and ignores duplicates.
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // ── Authentication — JWT Bearer via Identity module (OpenIddict) ──────────
        services.AddChassisAuthentication(config, builder.Environment);

        // ── MassTransit transport — selected by Dispatch:Transport configuration ──
        // Valid values:
        //   "inproc" (default) — MassTransit Mediator; no broker required; suitable for
        //                        local development and single-process deployments.
        //   "bus"              — MassTransit RabbitMQ Bus with EF Core Outbox/Inbox;
        //                        requires RabbitMq:Host/Username/Password in configuration.
        //
        // Handler code is identical under both modes — the transport switch changes only
        // how messages are dispatched and delivered, not what the handlers do.
        string transport = config["Dispatch:Transport"] ?? "inproc";

        if (string.Equals(transport, "bus", StringComparison.OrdinalIgnoreCase))
        {
            MassTransitConfig.AddChassisBus(services, config);
        }
        else
        {
            MassTransitConfig.AddChassisMediator(services);
        }

        // ── Module loader ──────────────────────────────────────────────────────────
        // Create the loader early (before Build()) so we can call ConfigureServices
        // on each module. The same instance is registered as the singleton so
        // UseChassisPipeline re-uses the cached load result — no double-scan.
        using ILoggerFactory tempLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var moduleLoader = new ReflectionModuleLoader(
            tempLoggerFactory.CreateLogger<ReflectionModuleLoader>());

        // Eagerly call Load() and cache the result inside the loader instance.
        IEnumerable<IModuleStartup> modules = moduleLoader.Load();
        foreach (IModuleStartup module in modules)
        {
            module.ConfigureServices(services, config);
        }

        // Register the same pre-loaded instance as singleton so UseChassisPipeline
        // re-uses the cached modules (Load() is idempotent after the first call).
        services.AddSingleton<IModuleLoader>(moduleLoader);

        // ── FluentValidation ───────────────────────────────────────────────────────
        // Individual modules register their own validators via AddValidatorsFromAssembly
        // in their ConfigureServices. The chassis host adds validators from its own assembly
        // (pipeline-internal validators, if any).
        // services.AddValidatorsFromAssemblyContaining<Program>(); — nothing to register yet.

        // ── OpenAPI ────────────────────────────────────────────────────────────────
        services.AddOpenApi();

        // ── Health checks ──────────────────────────────────────────────────────────
        services.AddHealthChecks();

        // ── OpenTelemetry — tracing + metrics exported via OTLP gRPC ─────────────
        services.AddChassisObservability(config, builder.Environment);

        // ── Outbox lag reporter — polls outbox table every 10s and records metrics ─
        services.AddHostedService<OutboxLagReporter>();

        // ── Rate limiting ──────────────────────────────────────────────────────────
        services.AddChassisRateLimiting(config);

        return builder;
    }

    /// <summary>
    /// Configures the ASP.NET Core middleware pipeline in the required order and
    /// maps all module endpoints discovered via <see cref="IModuleLoader"/>.
    /// </summary>
    public static WebApplication UseChassisPipeline(this WebApplication app)
    {
        // ── Middleware order — must match .claude/CLAUDE.md §Middleware Order ──────
        app.UseExceptionHandler();   // Must be first: catches all unhandled exceptions
        app.UseHsts();               // HSTS before any response writing
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseChassisSecurityHeaders();  // OWASP headers on all responses
        app.UseRateLimiter();             // Rate limiting before auth
        app.UseAuthentication();     // Must be before UseAuthorization
        app.UseAuthorization();

        // ── Tenant middleware — runs after auth so JWT claims are available ─────────
        app.UseMiddleware<TenantMiddleware>();

        // ── OpenAPI / Scalar ───────────────────────────────────────────────────────
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        // ── Health check endpoint (AllowAnonymous — no tenant required) ───────────
        app.MapHealthChecks("/health").AllowAnonymous();

        // ── Module endpoint discovery ──────────────────────────────────────────────
        IModuleLoader moduleLoader = app.Services.GetRequiredService<IModuleLoader>();
        foreach (IModuleStartup module in moduleLoader.Load())
        {
            module.Configure(app);
        }

        return app;
    }
}
