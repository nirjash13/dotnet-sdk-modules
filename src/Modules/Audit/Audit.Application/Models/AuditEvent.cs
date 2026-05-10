using System;

namespace Audit.Application.Models;

/// <summary>
/// Immutable record representing a single auditable action.
/// All fields are captured at the HTTP boundary; none are derived inside the handler.
/// </summary>
/// <param name="TenantId">Tenant that owns this event.</param>
/// <param name="ActorId">User or service account that performed the action (e.g. sub claim).</param>
/// <param name="Action">Action verb in dot-notation (e.g. "invoice.created", "user.password_reset").</param>
/// <param name="ResourceType">Entity type affected (e.g. "Invoice").</param>
/// <param name="ResourceId">Identifier of the affected entity.</param>
/// <param name="BeforeJson">JSON snapshot of the entity before the action; null for create operations.</param>
/// <param name="AfterJson">JSON snapshot of the entity after the action; null for delete operations.</param>
/// <param name="IpAddress">Client IP address.</param>
/// <param name="UserAgent">HTTP User-Agent header value.</param>
/// <param name="CorrelationId">Distributed tracing correlation identifier.</param>
/// <param name="Timestamp">When the event occurred (UTC). Defaults to UtcNow if not supplied.</param>
public record AuditEvent(
    Guid TenantId,
    string ActorId,
    string Action,
    string ResourceType,
    string ResourceId,
    string? BeforeJson = null,
    string? AfterJson = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null,
    DateTimeOffset? Timestamp = null);
