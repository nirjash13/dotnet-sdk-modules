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

    /// <summary>Gets options that control EF Core persistence behavior (e.g., automatic migration on startup).</summary>
    public SaasBuilderPersistenceOptions Persistence { get; } = new SaasBuilderPersistenceOptions();

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
    /// Selects the tenant isolation strategy. Shorthand for <c>opts.Tenancy.UseTenancy(isolation)</c>.
    /// For modes other than <see cref="TenantIsolation.PoolWithRls"/>, a startup warning is logged
    /// and <see cref="NotSupportedException"/> is thrown on first provider dispatch (not at startup).
    /// </summary>
    public SaasBuilderOptions UseTenancy(TenantIsolation isolation)
    {
        Tenancy.UseTenancy(isolation);
        return this;
    }
}
