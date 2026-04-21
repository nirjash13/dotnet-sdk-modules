using System;

namespace Ledger.Infrastructure.Events;

/// <summary>
/// Marten document type representing an immutable domain audit event.
/// Stored in the <c>ledger_events</c> schema with <c>TenancyStyle.Conjoined</c>
/// (a single table partitioned by <c>tenant_id</c>).
/// </summary>
public sealed class DomainAuditEvent
{
    /// <summary>Gets or sets the unique identifier of this audit event record.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant that owns this audit event.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the aggregate root identifier (e.g. Account.Id).</summary>
    public Guid AggregateId { get; set; }

    /// <summary>Gets or sets the string discriminator for the event type.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialised event payload (JSON string).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the event occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}
