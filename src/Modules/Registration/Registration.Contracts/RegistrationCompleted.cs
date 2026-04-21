using System;

namespace Registration.Contracts;

/// <summary>
/// Terminal integration event published by the saga when all provisioning steps
/// (<see cref="CreateUser"/>, <see cref="InitLedger"/>, <see cref="ProvisionReporting"/>) have succeeded.
/// </summary>
public class RegistrationCompleted
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the provisioned tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the created user identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the created ledger account identifier.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the created reporting bootstrap identifier.</summary>
    public Guid ReportingId { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the registration was completed.</summary>
    public DateTimeOffset CompletedAt { get; set; }
}
