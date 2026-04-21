using System;
using System.Threading;
using System.Threading.Tasks;
using Ledger.Application.Abstractions;
using Marten;

namespace Ledger.Infrastructure.Events;

/// <summary>
/// Marten-backed implementation of <see cref="IDomainAuditEventStore"/>.
/// Uses <c>TenancyStyle.Conjoined</c> — a single <c>ledger_events</c> schema table
/// with a <c>tenant_id</c> discriminator column.
/// </summary>
/// <remarks>
/// Events are stored as plain Marten documents (not event-sourcing streams) to keep
/// the showcase simple. Phase 5 can migrate to <c>IEventStore</c> streams if
/// event-sourcing replay is required.
/// </remarks>
internal sealed class MartenDomainAuditEventStore(IDocumentSession session)
    : IDomainAuditEventStore
{
    /// <inheritdoc />
    public Task AppendAsync(
        Guid tenantId,
        Guid aggregateId,
        string eventType,
        string payload,
        DateTimeOffset occurredAt,
        CancellationToken ct = default)
    {
        // Store is simply inserted; Marten buffers it until session.SaveChangesAsync().
        // The session is shared with LedgerUnitOfWork so the save happens atomically
        // inside the TransactionScope in LedgerUnitOfWork.CommitAsync().
        var auditEvent = new DomainAuditEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateId = aggregateId,
            EventType = eventType,
            Payload = payload,
            OccurredAt = occurredAt,
        };

        session.Store(auditEvent);

        // Store is synchronous in Marten — return completed task.
        return Task.CompletedTask;
    }
}
