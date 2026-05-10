using System;

namespace Audit.Contracts;

/// <summary>DTO returned by the audit events API.</summary>
/// <param name="Id">Event identifier.</param>
/// <param name="TenantId">Tenant that generated the event.</param>
/// <param name="ActorId">User or service account that performed the action.</param>
/// <param name="Action">Action verb (e.g. "invoice.created", "user.role_changed").</param>
/// <param name="ResourceType">Entity type affected (e.g. "Invoice", "User").</param>
/// <param name="ResourceId">Identifier of the affected entity.</param>
/// <param name="IpAddress">Client IP address at the time of the event.</param>
/// <param name="CorrelationId">Distributed tracing correlation identifier.</param>
/// <param name="Timestamp">When the event occurred (UTC).</param>
public record AuditEventDto(
    Guid Id,
    Guid TenantId,
    string ActorId,
    string Action,
    string ResourceType,
    string ResourceId,
    string? IpAddress,
    string? CorrelationId,
    DateTimeOffset Timestamp);
