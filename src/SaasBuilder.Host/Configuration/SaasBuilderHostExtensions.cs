using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaasBuilder.Host.Configuration.Options;
using SaasBuilder.Host.ErrorHandling;
using SaasBuilder.Host.Health;
using SaasBuilder.Host.Modules;
using SaasBuilder.Host.Observability;
using SaasBuilder.Host.RateLimiting;
using SaasBuilder.Host.Tenancy;
using SaasBuilder.Host.Transport;
using SaasBuilder.Persistence.Migrations;
using SaasBuilder.Persistence.Tenancy;
using SaasBuilder.Persistence.Tenancy.Lifecycle;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Configuration;
using SaasBuilder.SharedKernel.Tenancy;
using SaasBuilder.SharedKernel.Tenancy.Lifecycle;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace SaasBuilder.Host.Configuration;

/// <summary>
/// Extension methods that compose the full SaasBuilder host from its constituent parts.
/// </summary>
public static class SaasBuilderHostExtensions
{
    /// <summary>
    /// Registers all chassis services using the fluent <see cref="SaasBuilderOptions"/> API.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="SaasBuilderOptions"/>. When <c>null</c>
    /// all defaults are used (InProc transport, PoolWithRls tenancy, observability enabled,
    /// rate limiting enabled, legacy base-directory module scan).
    /// </param>
    /// <example>
    /// <code>
    /// builder.AddSaasBuilderHost(opts =>
    /// {
    ///     opts.UseTransport(SaasTransport.InProc);
    ///     opts.UseTenancy(TenantIsolation.PoolWithRls);
    ///     opts.Modules.AddProbeDirectory("modules/");
    ///     opts.Observability.Enable();
    ///     opts.RateLimiting.UsePerTenantSlidingWindow();
    /// });
    /// </code>
    /// </example>
    public static WebApplicationBuilder AddSaasBuilderHost(
        this WebApplicationBuilder builder,
        Action<SaasBuilderOptions>? configure = null)
    {
        var options = new SaasBuilderOptions();
        configure?.Invoke(options);

        IServiceCollection services = builder.Services;
        IConfiguration config = builder.Configuration;

        // ── Serilog — configure before any other service that might log ────────────
        // UseSerilog() replaces the default Microsoft.Extensions.Logging pipeline with
        // Serilog. The logger is also forwarded to the OTel Collector via OTLP.
        // Shutdown/flush is handled automatically by UseSerilog on host stop.
        string otlpEndpoint = config["Otel:Endpoint"] ?? "http://localhost:4317";

        // Register TenantLogEnricher in DI so it can resolve ITenantContextAccessor.
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
        // NOTE: If AddSaasBuilderPersistence<T> is called by a module, it will also call
        // AddSingleton<ITenantContextAccessor, TenantContextAccessor>. AddSingleton is
        // idempotent when the same concrete type is registered — the DI container
        // honours the first registration and ignores duplicates.
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // ── Tenant isolation — register ITenantResourcesProvider for the chosen mode ──
        // For deferred modes (non-PoolWithRls), log a startup warning and register the stub.
        // The stub throws NotSupportedException on first dispatch, not at startup.
        RegisterTenantResourcesProvider(services, options.Tenancy);

        // ── Tenant resolver pipeline ───────────────────────────────────────────────
        // Register all configured resolvers. UseDefaults() applies the built-in set when
        // the caller has not explicitly configured any resolvers.
        options.Tenancy.Resolvers.UseDefaults(services);

        // ── TenantMiddleware options — snapshot anonymous bypass list ─────────────
        // Convert the mutable ISet to an immutable snapshot for the middleware.
        // Registered as singleton so TenantMiddleware constructor resolves IOptions<TenantMiddlewareOptions>.
        var middlewareOptions = new Tenancy.TenantMiddlewareOptions
        {
            AnonymousBypass = new System.Collections.Generic.HashSet<string>(
                options.Tenancy.AnonymousBypass,
                StringComparer.OrdinalIgnoreCase),
        };
        services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(middlewareOptions));

        // ── Tenant lifecycle service ──────────────────────────────────────────────
        services.AddScoped<ITenantLifecycleService, TenantLifecycleService>();

        // ── Authentication — JWT Bearer via Identity module (OpenIddict) ──────────
        services.AddSaasBuilderAuthentication(config, builder.Environment);

        // ── MassTransit transport — selected by options (or config fallback) ───────
        // Options-driven selection takes precedence. When the caller has not explicitly
        // configured transport, the legacy config key (Dispatch:Transport) is checked for
        // backward compatibility with existing deployments.
        SaasTransport transport = options.Transport.Transport;

        if (transport == SaasTransport.InProc)
        {
            // Only consult config when options is at its default — an explicit UseInProc()
            // call would still be InProc, but we skip the config check in that case.
            // Since we cannot distinguish "caller called UseInProc()" from "never touched",
            // the config key can only promote InProc → Bus (never demote Bus → InProc).
            string configTransport = config["Dispatch:Transport"] ?? "inproc";
            if (string.Equals(configTransport, "bus", StringComparison.OrdinalIgnoreCase))
            {
                transport = SaasTransport.Bus;
            }
        }

        if (transport == SaasTransport.Bus)
        {
            MassTransitConfig.AddSaasBuilderBus(services, config, options.Transport.BusConsumers);
        }
        else
        {
            MassTransitConfig.AddSaasBuilderMediator(services, options.Transport.MediatorConsumers);
        }

        // ── Module loader ──────────────────────────────────────────────────────────
        // Create the loader early (before Build()) so we can call ConfigureServices
        // on each module. The same instance is registered as the singleton so
        // UseSaasBuilderPipeline re-uses the cached load result — no double-scan.
        using ILoggerFactory tempLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        ILogger<ReflectionModuleLoader> moduleLogger =
            tempLoggerFactory.CreateLogger<ReflectionModuleLoader>();

        var moduleLoader = options.Modules.HasExplicitSources
            ? new ReflectionModuleLoader(moduleLogger, options.Modules)
            : new ReflectionModuleLoader(moduleLogger);

        // Eagerly call Load() and cache the result inside the loader instance.
        IEnumerable<IModuleStartup> modules = moduleLoader.Load();
        foreach (IModuleStartup module in modules)
        {
            module.ConfigureServices(services, config);
        }

        // Register the same pre-loaded instance as singleton so UseSaasBuilderPipeline
        // re-uses the cached modules (Load() is idempotent after the first call).
        services.AddSingleton<IModuleLoader>(moduleLoader);

        // ── OpenAPI ────────────────────────────────────────────────────────────────
        services.AddOpenApi();

        // ── Health checks ──────────────────────────────────────────────────────────
        // Readiness checks are tagged "ready"; startup checks are tagged "startup".
        // Liveness (/health/live) runs no checks — it is always 200.
        services.AddHealthChecks()
            .AddCheck<StartupHealthCheck>("startup", tags: new[] { "startup" })
            .AddCheck<ReadinessHealthCheck>("readiness", tags: new[] { "ready" });

        // ── OpenTelemetry — tracing + metrics exported via OTLP gRPC ─────────────
        if (options.Observability.IsEnabled)
        {
            services.AddSaasBuilderObservability(config, builder.Environment);

            // ── Outbox lag reporter — polls outbox table every 10s and records metrics ─
            services.AddHostedService<OutboxLagReporter>();
        }

        // ── Rate limiting ──────────────────────────────────────────────────────────
        if (options.RateLimiting.IsEnabled)
        {
            if (options.RateLimiting.UsePerTenantWindow)
            {
                services.AddPerTenantSlidingWindowRateLimiting(config);
            }
            else
            {
                services.AddSaasBuilderRateLimiting(config);
            }
        }

        // ── Migration on startup ───────────────────────────────────────────────────
        if (options.Persistence.MigrateOnStartup)
        {
            services.AddScoped<IMigrationRunner, PostgresAdvisoryLockMigrationRunner>();
            services.AddHostedService<MigrationStartupService>();
        }

        return builder;
    }

    /// <summary>
    /// Configures the ASP.NET Core middleware pipeline in the required order and
    /// maps all module endpoints discovered via <see cref="IModuleLoader"/>.
    /// </summary>
    public static WebApplication UseSaasBuilderPipeline(this WebApplication app)
    {
        // ── Middleware order — must match .claude/CLAUDE.md §Middleware Order ──────
        app.UseExceptionHandler();   // Must be first: catches all unhandled exceptions
        app.UseHsts();               // HSTS before any response writing
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSaasBuilderSecurityHeaders();  // OWASP headers on all responses
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

        // ── Health probe endpoints (liveness / readiness / startup + legacy /health) ─
        app.MapSaasBuilderHealthEndpoints();

        // ── Module endpoint discovery ──────────────────────────────────────────────
        IModuleLoader moduleLoader = app.Services.GetRequiredService<IModuleLoader>();
        foreach (IModuleStartup module in moduleLoader.Load())
        {
            module.Configure(app);
        }

        return app;
    }

    /// <summary>
    /// Registers the appropriate <see cref="ITenantResourcesProvider"/> for the configured
    /// isolation mode. For deferred modes, registers the stub and logs a warning.
    /// </summary>
    private static void RegisterTenantResourcesProvider(
        IServiceCollection services,
        SaasBuilderTenancyOptions tenancyOptions)
    {
        switch (tenancyOptions.Isolation)
        {
            case TenantIsolation.PoolWithRls:
                services.AddScoped<ITenantResourcesProvider, PoolWithRlsTenantResourcesProvider>();
                break;

            case TenantIsolation.PoolShared:
                LogDeferredIsolationMode(services, tenancyOptions.Isolation);
                services.AddScoped<ITenantResourcesProvider, PoolSharedTenantResourcesProvider>();
                break;

            case TenantIsolation.SiloedSchema:
                LogDeferredIsolationMode(services, tenancyOptions.Isolation);
                services.AddScoped<ITenantResourcesProvider, SiloedSchemaTenantResourcesProvider>();
                break;

            case TenantIsolation.SiloedDatabase:
                LogDeferredIsolationMode(services, tenancyOptions.Isolation);
                services.AddScoped<ITenantResourcesProvider, SiloedDatabaseTenantResourcesProvider>();
                break;

            case TenantIsolation.SiloedStamp:
                LogDeferredIsolationMode(services, tenancyOptions.Isolation);
                services.AddScoped<ITenantResourcesProvider, SiloedStampTenantResourcesProvider>();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(tenancyOptions),
                    tenancyOptions.Isolation,
                    "Unknown TenantIsolation value.");
        }
    }

    private static void LogDeferredIsolationMode(
        IServiceCollection services,
        TenantIsolation isolation)
    {
        // Log the warning via a startup filter so the real ILogger<T> is available.
        // We cannot use ILogger here because the DI container is not yet built.
        // Instead, register a no-op startup action that logs once during application startup.
        services.AddSingleton(new DeferredIsolationWarning(isolation));
        services.AddHostedService<DeferredIsolationWarningLogger>();
    }
}
