using System;

namespace Admin.Application.Models;

/// <summary>
/// Represents an admin action that has been gated pending second-admin approval.
/// </summary>
public sealed class PendingAdminAction
{
    /// <summary>Gets or sets the unique action record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the requesting admin actor identifier.</summary>
    public string RequestorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the action name (e.g., "tenant.impersonate").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the target tenant identifier.</summary>
    public Guid? TargetTenantId { get; set; }

    /// <summary>Gets or sets the serialized action payload.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Gets or sets the sensitivity level of this action.</summary>
    public SensitivityLevel Sensitivity { get; set; }

    /// <summary>Gets or sets when the action was requested.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Gets or sets the current approval status.</summary>
    public PendingActionStatus Status { get; set; }

    /// <summary>Gets or sets the approver identifier (populated after resolution).</summary>
    public string? ApproverId { get; set; }

    /// <summary>Gets or sets when the action was approved or denied.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Gets or sets the denial reason, when denied.</summary>
    public string? DenialReason { get; set; }
}

/// <summary>
/// Sensitivity classification driving approval requirements.
/// </summary>
public enum SensitivityLevel
{
    /// <summary>Low-sensitivity action — no approval required.</summary>
    Low = 0,

    /// <summary>High-sensitivity action — requires second-admin approval.</summary>
    High = 1,
}

/// <summary>
/// Status of a pending admin action.
/// </summary>
public enum PendingActionStatus
{
    /// <summary>Awaiting second-admin approval.</summary>
    Pending = 0,

    /// <summary>Approved and executed.</summary>
    Approved = 1,

    /// <summary>Denied by second admin.</summary>
    Denied = 2,
}
