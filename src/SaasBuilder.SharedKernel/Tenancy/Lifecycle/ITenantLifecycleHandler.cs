using System;
using System.Threading;
using System.Threading.Tasks;

namespace SaasBuilder.SharedKernel.Tenancy.Lifecycle;

/// <summary>
/// Participates in tenant lifecycle state transitions.
/// Register implementations in DI via <c>services.AddScoped&lt;ITenantLifecycleHandler, MyHandler&gt;()</c>;
/// the <see cref="ITenantLifecycleService"/> will fan out calls to all registered handlers.
/// </summary>
/// <remarks>
/// Implementations MUST be idempotent — handlers may be called more than once if a previous
/// attempt is retried after a transient failure.
/// </remarks>
public interface ITenantLifecycleHandler
{
    /// <summary>
    /// Called when a new tenant is being provisioned (resources created, roles seeded).
    /// </summary>
    /// <remarks>TODO(Phase 3): Provisioning workflow is deferred — see SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.2.</remarks>
    /// <param name="tenantId">The tenant being provisioned.</param>
    /// <param name="ct">A cancellation token.</param>
    Task OnProvisionAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Called when a tenant is being suspended (e.g., payment failure).
    /// Handlers should block writes and return 423/402 for tenant-scoped operations.
    /// </summary>
    /// <remarks>TODO(Phase 3): Suspension workflow is deferred — see SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.2.</remarks>
    /// <param name="tenantId">The tenant being suspended.</param>
    /// <param name="ct">A cancellation token.</param>
    Task OnSuspendAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Called when a tenant is being archived (data exported, read-only mode).
    /// </summary>
    /// <remarks>TODO(Phase 3): Archive workflow is deferred — see SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.2.</remarks>
    /// <param name="tenantId">The tenant being archived.</param>
    /// <param name="ct">A cancellation token.</param>
    Task OnArchiveAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Called when a tenant is permanently deleted after the retention period.
    /// </summary>
    /// <remarks>TODO(Phase 3): Delete workflow is deferred — see SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.2.</remarks>
    /// <param name="tenantId">The tenant being deleted.</param>
    /// <param name="ct">A cancellation token.</param>
    Task OnDeleteAsync(Guid tenantId, CancellationToken ct = default);
}
