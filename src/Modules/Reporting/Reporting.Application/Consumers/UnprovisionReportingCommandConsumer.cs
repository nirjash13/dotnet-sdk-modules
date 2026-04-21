using System.Threading.Tasks;
using MassTransit;
using Registration.Contracts;

namespace Reporting.Application.Consumers;

/// <summary>
/// Compensation handler for <see cref="UnprovisionReporting"/>.
/// Removes any tenant-specific reporting bootstrap state. Phase 5 stub: no-op since
/// <see cref="ProvisionReportingCommandConsumer"/> does not write persistent state.
/// Responds with a success acknowledgement to advance the saga.
/// </summary>
/// <remarks>
/// Idempotent: calling this multiple times is safe — if no state was written, nothing is removed.
/// </remarks>
public sealed class UnprovisionReportingCommandConsumer : IConsumer<UnprovisionReporting>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<UnprovisionReporting> context)
    {
        // Phase 5 stub: ProvisionReporting is a no-op, so no rollback is needed.
        // Phase 6+: query and remove the tenant reporting config row here.

        // NOTE: UnprovisionReporting has no dedicated response message in the current saga design
        // because the saga transitions to Faulted after the ledger rollback + user delete sequence,
        // not after the reporting unprovision. This consumer is retained for completeness and future
        // saga enhancement (e.g. a 4-step compensation chain).
        return Task.CompletedTask;
    }
}
