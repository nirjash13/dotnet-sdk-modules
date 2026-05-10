using System;
using SaasBuilder.SharedKernel.Tenancy;

namespace Audit.Infrastructure.Entities;

/// <summary>
/// EF Core entity persisted to the append-only <c>audit_entries</c> table.
/// Once inserted, rows must never be updated or deleted (enforced at DB level via privilege revocation).
/// </summary>
public sealed class AuditEntry : ITenantScoped
{
    // EF Core parameterless constructor.
    private AuditEntry()
    {
        ActorId = string.Empty;
        Action = string.Empty;
        ResourceType = string.Empty;
        ResourceId = string.Empty;
    }

    /// <summary>Initializes a new audit entry.</summary>
    public AuditEntry(
        Guid id,
        Guid tenantId,
        string actorId,
        string action,
        string resourceType,
        string resourceId,
        string? beforeJson,
        string? afterJson,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        DateTimeOffset timestamp,
        string? prevHash = null,
        string? hash = null)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        Timestamp = timestamp;
        PrevHash = prevHash;
        Hash = hash;
    }

    /// <summary>Gets the entry identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the actor identifier (user sub or service account name).</summary>
    public string ActorId { get; private set; }

    /// <summary>Gets the action verb (e.g. "invoice.created").</summary>
    public string Action { get; private set; }

    /// <summary>Gets the entity type affected.</summary>
    public string ResourceType { get; private set; }

    /// <summary>Gets the entity identifier affected.</summary>
    public string ResourceId { get; private set; }

    /// <summary>Gets the JSON snapshot before the action (null for creates).</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>Gets the JSON snapshot after the action (null for deletes).</summary>
    public string? AfterJson { get; private set; }

    /// <summary>Gets the client IP address.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the HTTP User-Agent.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Gets the distributed tracing correlation identifier.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>Gets the event timestamp (UTC).</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Gets the SHA-256 hash of the previous entry in the chain (null for the first entry).</summary>
    public string? PrevHash { get; private set; }

    /// <summary>Gets the SHA-256 hash of this entry (used to verify chain integrity).</summary>
    public string? Hash { get; private set; }
}
