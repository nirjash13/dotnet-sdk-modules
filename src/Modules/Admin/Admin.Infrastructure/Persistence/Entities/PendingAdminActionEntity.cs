using System;
using Admin.Application.Models;

namespace Admin.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for a pending admin action awaiting second-admin approval.
/// </summary>
public sealed class PendingAdminActionEntity
{
    private PendingAdminActionEntity()
    {
    }

    /// <summary>Gets the record identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the requesting admin actor identifier.</summary>
    public string RequestorId { get; private set; } = string.Empty;

    /// <summary>Gets the action name.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Gets the target tenant identifier.</summary>
    public Guid? TargetTenantId { get; private set; }

    /// <summary>Gets the serialized action payload.</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>Gets the sensitivity level.</summary>
    public SensitivityLevel Sensitivity { get; private set; }

    /// <summary>Gets when the action was requested.</summary>
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>Gets the current approval status.</summary>
    public PendingActionStatus Status { get; internal set; }

    /// <summary>Gets the approver identifier (populated after resolution).</summary>
    public string? ApproverId { get; internal set; }

    /// <summary>Gets when the action was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; internal set; }

    /// <summary>Gets the denial reason, when denied.</summary>
    public string? DenialReason { get; internal set; }

    /// <summary>Creates a new pending action entity from the domain model.</summary>
    public static PendingAdminActionEntity Create(PendingAdminAction action) =>
        new PendingAdminActionEntity
        {
            Id = action.Id == Guid.Empty ? Guid.NewGuid() : action.Id,
            RequestorId = action.RequestorId,
            Action = action.Action,
            TargetTenantId = action.TargetTenantId,
            PayloadJson = action.PayloadJson,
            Sensitivity = action.Sensitivity,
            RequestedAt = action.RequestedAt == default ? DateTimeOffset.UtcNow : action.RequestedAt,
            Status = PendingActionStatus.Pending,
        };

    /// <summary>Maps the entity back to the domain model.</summary>
    public PendingAdminAction ToDomain() => new PendingAdminAction
    {
        Id = Id,
        RequestorId = RequestorId,
        Action = Action,
        TargetTenantId = TargetTenantId,
        PayloadJson = PayloadJson,
        Sensitivity = Sensitivity,
        RequestedAt = RequestedAt,
        Status = Status,
        ApproverId = ApproverId,
        ResolvedAt = ResolvedAt,
        DenialReason = DenialReason,
    };
}
