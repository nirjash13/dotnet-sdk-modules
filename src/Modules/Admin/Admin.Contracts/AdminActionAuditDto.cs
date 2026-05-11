using System;

namespace Admin.Contracts;

/// <summary>
/// DTO representing a single admin action audit record.
/// </summary>
public sealed class AdminActionAuditDto
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the admin actor identifier (sub claim).</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the action performed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the target tenant identifier.</summary>
    public Guid? TargetTenantId { get; set; }

    /// <summary>Gets or sets the JSON payload of the request (PII fields redacted).</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Gets or sets the client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the HTTP User-Agent header.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Gets or sets when the action occurred.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Gets or sets whether the actor was a system admin.</summary>
    public bool IsSystemAdmin { get; set; }
}
