using System;

namespace Admin.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity representing a single admin action audit record.
/// This table is append-only — no UPDATEs or DELETEs in production.
/// </summary>
public sealed class AdminActionAuditEntry
{
    private AdminActionAuditEntry()
    {
    }

    /// <summary>Gets the record identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the admin actor identifier (sub claim).</summary>
    public string ActorId { get; private set; } = string.Empty;

    /// <summary>Gets the action performed in dot-notation.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Gets the target tenant identifier, if applicable.</summary>
    public Guid? TargetTenantId { get; private set; }

    /// <summary>Gets the serialized (PII-redacted) request payload.</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>Gets the client IP address.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the HTTP User-Agent header.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Gets when the action occurred (UTC).</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Gets whether the actor was a system admin.</summary>
    public bool IsSystemAdmin { get; private set; }

    /// <summary>Creates a new audit entry.</summary>
    public static AdminActionAuditEntry Create(
        string actorId,
        string action,
        Guid? targetTenantId,
        string? payloadJson,
        string? ipAddress,
        string? userAgent,
        bool isSystemAdmin = true) =>
        new AdminActionAuditEntry
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            TargetTenantId = targetTenantId,
            PayloadJson = payloadJson,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTimeOffset.UtcNow,
            IsSystemAdmin = isSystemAdmin,
        };
}
