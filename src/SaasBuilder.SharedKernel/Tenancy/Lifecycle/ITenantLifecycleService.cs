using System;
using System.Threading;
using System.Threading.Tasks;

namespace SaasBuilder.SharedKernel.Tenancy.Lifecycle;

/// <summary>
/// Orchestrates tenant lifecycle transitions by fanning out to all registered
/// <see cref="ITenantLifecycleHandler"/> implementations in module registration order.
/// </summary>
/// <remarks>
/// The orchestrator is responsible for:
/// <list type="bullet">
///   <item>Invoking each handler in sequence (module order determined by DI registration).</item>
///   <item>Wrapping handler failures in <see cref="TenantLifecycleException"/> with the inner exception preserved.</item>
///   <item>Ensuring that a handler failure does not skip remaining handlers (fail-fast vs. fan-out policy is implementation-defined).</item>
/// </list>
/// </remarks>
public interface ITenantLifecycleService
{
    /// <summary>Transitions the tenant to <see cref="TenantStatus.Active"/> after provisioning completes.</summary>
    /// <param name="tenantId">The tenant to provision.</param>
    /// <param name="ct">A cancellation token.</param>
    Task ProvisionAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Transitions the tenant to <see cref="TenantStatus.Suspended"/>.</summary>
    /// <param name="tenantId">The tenant to suspend.</param>
    /// <param name="ct">A cancellation token.</param>
    Task SuspendAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Transitions the tenant to <see cref="TenantStatus.Archived"/>.</summary>
    /// <param name="tenantId">The tenant to archive.</param>
    /// <param name="ct">A cancellation token.</param>
    Task ArchiveAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Transitions the tenant to <see cref="TenantStatus.Deleted"/> (permanent, irreversible).</summary>
    /// <param name="tenantId">The tenant to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    Task DeleteAsync(Guid tenantId, CancellationToken ct = default);
}
