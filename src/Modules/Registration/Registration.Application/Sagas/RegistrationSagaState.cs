using System;
using MassTransit;

namespace Registration.Application.Sagas;

/// <summary>
/// Persisted state for the association registration saga.
/// </summary>
/// <remarks>
/// IMPORTANT: This entity does NOT implement <c>ITenantScoped</c>.
/// The saga state is intentionally tenant-agnostic because the tenant being provisioned
/// does not yet exist when the saga starts. RLS is not applied to the saga state table.
/// See <c>migrations/registration/001_initial_registration.sql</c> for the SQL comment.
/// </remarks>
public sealed class RegistrationSagaState : SagaStateMachineInstance
{
    /// <summary>Gets or sets the saga correlation identifier (primary key).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the current state name (serialised by MassTransit).</summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant identifier being provisioned.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the association name.</summary>
    public string AssociationName { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary user's email address.</summary>
    public string PrimaryUserEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency code for the ledger.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the created user identifier, populated after <c>UserCreated</c>.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Gets or sets the created account identifier, populated after <c>LedgerInitialized</c>.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Gets or sets the reporting bootstrap identifier, populated after <c>ReportingProvisioned</c>.</summary>
    public Guid? ReportingId { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the saga was started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the saga reached a terminal state.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the human-readable reason for failure, set when the saga faults.</summary>
    public string? FailureReason { get; set; }
}
