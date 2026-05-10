namespace SaasBuilder.SharedKernel.Configuration;

/// <summary>
/// Tenant data isolation strategy. Only <see cref="PoolWithRls"/> is fully implemented;
/// the remaining modes throw <see cref="System.NotSupportedException"/> with a TODO
/// referencing Phase 3 of the roadmap.
/// </summary>
public enum TenantIsolation
{
    /// <summary>
    /// Shared connection pool, no row-level isolation. Suitable for non-sensitive B2C data.
    /// </summary>
    /// <remarks>TODO Phase 3: implement PoolShared mode.</remarks>
    PoolShared = 0,

    /// <summary>
    /// Shared connection pool with PostgreSQL Row-Level Security policies.
    /// This is the default and the only fully-implemented isolation mode in Phase 1.
    /// </summary>
    PoolWithRls = 1,

    /// <summary>
    /// One schema per tenant within a shared database.
    /// </summary>
    /// <remarks>TODO Phase 3: implement SiloedSchema mode.</remarks>
    SiloedSchema = 2,

    /// <summary>
    /// One database per tenant. Connection strings are resolved via <c>ITenantResources</c>.
    /// </summary>
    /// <remarks>TODO Phase 3: implement SiloedDatabase mode.</remarks>
    SiloedDatabase = 3,

    /// <summary>
    /// One regional deployment (stamp) per tenant. Routing via <c>IStampRouter</c>.
    /// </summary>
    /// <remarks>TODO Phase 3: implement SiloedStamp mode.</remarks>
    SiloedStamp = 4,
}
