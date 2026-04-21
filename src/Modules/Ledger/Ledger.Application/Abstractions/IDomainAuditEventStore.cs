using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ledger.Application.Abstractions;

/// <summary>
/// Abstraction for the Marten-backed domain audit event store.
/// Implemented by <c>MartenDomainAuditEventStore</c> in Ledger.Infrastructure.
/// </summary>
public interface IDomainAuditEventStore
{
    /// <summary>
    /// Appends a domain audit event to the store. The event is buffered in the
    /// Marten document session and saved atomically when the unit of work commits.
    /// </summary>
    /// <param name="tenantId">The tenant context for this event.</param>
    /// <param name="aggregateId">The aggregate root identifier (e.g. Account.Id).</param>
    /// <param name="eventType">A string discriminator for the event type (e.g. "LedgerTransactionPosted").</param>
    /// <param name="payload">The serialised event payload (JSON string).</param>
    /// <param name="occurredAt">The UTC timestamp when the event occurred.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendAsync(
        Guid tenantId,
        Guid aggregateId,
        string eventType,
        string payload,
        DateTimeOffset occurredAt,
        CancellationToken ct = default);
}
