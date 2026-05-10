using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy.Lifecycle;

namespace SaasBuilder.Persistence.Tenancy.Lifecycle;

/// <summary>
/// Default <see cref="ITenantLifecycleService"/> implementation that fans out lifecycle
/// transitions to all registered <see cref="ITenantLifecycleHandler"/> implementations
/// in DI registration order.
/// </summary>
/// <remarks>
/// <para>
/// Handlers are invoked sequentially. If any handler throws, a <see cref="TenantLifecycleException"/>
/// is raised immediately and subsequent handlers are skipped (fail-fast). This ensures that
/// partial state is not silently accumulated when a handler cannot complete its work.
/// </para>
/// <para>
/// TODO(Phase 3.2): Provisioning, suspension, archive, and delete workflow IMPLEMENTATIONS
/// are deferred. The orchestrator and interfaces are the deliverable for the scaffold phase.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.2.
/// </para>
/// </remarks>
public sealed class TenantLifecycleService : ITenantLifecycleService
{
    private readonly IEnumerable<ITenantLifecycleHandler> _handlers;

    /// <summary>
    /// Initializes the service with all registered lifecycle handlers.
    /// </summary>
    /// <param name="handlers">
    /// All <see cref="ITenantLifecycleHandler"/> implementations registered in DI.
    /// The enumerable may be empty when no module has registered a handler.
    /// </param>
    public TenantLifecycleService(IEnumerable<ITenantLifecycleHandler> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    /// <inheritdoc />
    public async Task ProvisionAsync(Guid tenantId, CancellationToken ct = default)
        => await FanOutAsync(tenantId, "Provision", h => h.OnProvisionAsync(tenantId, ct)).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SuspendAsync(Guid tenantId, CancellationToken ct = default)
        => await FanOutAsync(tenantId, "Suspend", h => h.OnSuspendAsync(tenantId, ct)).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ArchiveAsync(Guid tenantId, CancellationToken ct = default)
        => await FanOutAsync(tenantId, "Archive", h => h.OnArchiveAsync(tenantId, ct)).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task DeleteAsync(Guid tenantId, CancellationToken ct = default)
        => await FanOutAsync(tenantId, "Delete", h => h.OnDeleteAsync(tenantId, ct)).ConfigureAwait(false);

    private async Task FanOutAsync(
        Guid tenantId,
        string operation,
        Func<ITenantLifecycleHandler, Task> invoke)
    {
        foreach (ITenantLifecycleHandler handler in _handlers)
        {
            try
            {
                await invoke(handler).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not TenantLifecycleException)
            {
                throw new TenantLifecycleException(
                    tenantId,
                    operation,
                    handler.GetType().FullName ?? handler.GetType().Name,
                    ex);
            }
        }
    }
}
