using System;
using SaasBuilder.SharedKernel.Configuration;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Root options for the SaasBuilder host composition. Pass to
/// <see cref="SaasBuilderHostExtensions.AddSaasBuilderHost(Microsoft.AspNetCore.Builder.WebApplicationBuilder, System.Action{SaasBuilderOptions}?)"/>
/// to configure transport, tenancy, observability, rate limiting, and module discovery.
/// </summary>
/// <example>
/// <code>
/// builder.AddSaasBuilderHost(opts =>
/// {
///     opts.UseTransport(SaasTransport.InProc);
///     opts.UseTenancy(TenantIsolation.PoolWithRls);
///     opts.Modules.ScanAssemblyContaining&lt;MyModule&gt;();
///     opts.Modules.AddProbeDirectory("modules/");
///     opts.Observability.Enable();
///     opts.RateLimiting.UsePerTenantSlidingWindow();
/// });
/// </code>
/// </example>
public sealed class SaasBuilderOptions
{
    /// <summary>Gets options that control module discovery.</summary>
    public SaasBuilderModulesOptions Modules { get; } = new SaasBuilderModulesOptions();

    /// <summary>Gets options that control OpenTelemetry observability.</summary>
    public SaasBuilderObservabilityOptions Observability { get; } = new SaasBuilderObservabilityOptions();

    /// <summary>Gets options that control the MassTransit transport.</summary>
    public SaasBuilderTransportOptions Transport { get; } = new SaasBuilderTransportOptions();

    /// <summary>Gets options that control tenant data isolation.</summary>
    public SaasBuilderTenancyOptions Tenancy { get; } = new SaasBuilderTenancyOptions();

    /// <summary>Gets options that control rate limiting.</summary>
    public SaasBuilderRateLimitingOptions RateLimiting { get; } = new SaasBuilderRateLimitingOptions();

    /// <summary>
    /// Selects the MassTransit transport. Shorthand for <c>opts.Transport.Use*()</c>.
    /// </summary>
    public SaasBuilderOptions UseTransport(SaasTransport transport)
    {
        if (transport == SaasTransport.Bus)
        {
            Transport.UseBus();
        }
        else
        {
            Transport.UseInProc();
        }

        return this;
    }

    /// <summary>
    /// Selects the tenant isolation strategy. Shorthand for <c>opts.Tenancy.Use*()</c>.
    /// Throws <see cref="NotSupportedException"/> for isolation modes not yet implemented.
    /// </summary>
    public SaasBuilderOptions UseTenancy(TenantIsolation isolation)
    {
        // Store and validate — AssertSupported is called later during host build.
        if (isolation != TenantIsolation.PoolWithRls)
        {
            throw new NotSupportedException(
                $"TenantIsolation.{isolation} is not yet implemented. " +
                "Only PoolWithRls is supported in Phase 1. " +
                "See Phase 3 of the roadmap for additional isolation modes.");
        }

        Tenancy.UsePoolWithRls();
        return this;
    }
}
