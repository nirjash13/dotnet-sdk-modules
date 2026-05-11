using System;
using System.Collections.Generic;
using SaasBuilder.SharedKernel.Configuration;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling the tenant data isolation strategy and the resolver pipeline.
/// </summary>
public sealed class SaasBuilderTenancyOptions
{
    /// <summary>
    /// Gets the selected isolation mode.
    /// Defaults to <see cref="TenantIsolation.PoolWithRls"/> (the only fully-implemented mode in Phase 1).
    /// </summary>
    public TenantIsolation Isolation { get; private set; } = TenantIsolation.PoolWithRls;

    /// <summary>
    /// Gets the tenant resolver pipeline options.
    /// </summary>
    public TenantResolverOptions Resolvers { get; } = new TenantResolverOptions();

    /// <summary>
    /// Gets the set of path prefixes that bypass tenant enforcement.
    /// Requests whose path starts with any of these values are allowed without a tenant context.
    /// </summary>
    /// <remarks>
    /// Defaults: <c>/health</c>, <c>/openapi</c>, <c>/.well-known</c>, <c>/connect</c>, <c>/scalar</c>.
    /// Additional paths can be appended by callers; the default set is always present.
    /// </remarks>
    public ISet<string> AnonymousBypass { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/openapi",
        "/.well-known",
        "/connect",
        "/scalar",
    };

    /// <summary>
    /// Sets the isolation strategy to <see cref="TenantIsolation.PoolWithRls"/> (default).
    /// </summary>
    public SaasBuilderTenancyOptions UsePoolWithRls()
    {
        Isolation = TenantIsolation.PoolWithRls;
        return this;
    }

    /// <summary>
    /// Sets the isolation strategy to the specified mode.
    /// For modes other than <see cref="TenantIsolation.PoolWithRls"/>, a startup warning
    /// is logged and <see cref="NotSupportedException"/> is thrown on first dispatch.
    /// </summary>
    /// <param name="isolation">The desired isolation mode.</param>
    public SaasBuilderTenancyOptions UseTenancy(TenantIsolation isolation)
    {
        Isolation = isolation;
        return this;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the chosen isolation mode is fully implemented.
    /// Deferred modes log a startup warning but do not fail until first dispatch.
    /// </summary>
    /// <remarks>
    /// All five modes are implemented as of Phase 3.
    /// <see cref="TenantIsolation.SiloedStamp"/> is a pass-through stub that logs a warning;
    /// the caller is expected to override with a custom <c>ITenantResourcesProvider</c>.
    /// </remarks>
    internal bool IsIsolationModeSupported() => Isolation switch
    {
        TenantIsolation.PoolWithRls => true,
        TenantIsolation.PoolShared => true,
        TenantIsolation.SiloedSchema => true,
        TenantIsolation.SiloedDatabase => true,
        TenantIsolation.SiloedStamp => true,
        _ => false,
    };

    /// <summary>
    /// Validates that the chosen isolation mode is supported.
    /// Throws <see cref="NotSupportedException"/> for unknown modes.
    /// </summary>
    internal void AssertSupported()
    {
        if (!IsIsolationModeSupported())
        {
            throw new NotSupportedException(
                $"TenantIsolation.{Isolation} is not a recognised isolation mode.");
        }
    }
}
