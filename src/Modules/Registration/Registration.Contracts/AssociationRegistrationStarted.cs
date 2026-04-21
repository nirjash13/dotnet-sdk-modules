using System;

namespace Registration.Contracts;

/// <summary>
/// Integration event that triggers the registration saga.
/// Published by the Registration API endpoint when a new association sign-up is received.
/// The saga correlates all subsequent commands and responses by <see cref="CorrelationId"/>.
/// </summary>
public class AssociationRegistrationStarted
{
    /// <summary>Gets or sets the unique message identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier that will be provisioned.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the human-readable association name.</summary>
    public string AssociationName { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary user's email address.</summary>
    public string PrimaryUserEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO 4217 currency code for the ledger (e.g. "USD").</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the registration was initiated.</summary>
    public DateTimeOffset StartedAt { get; set; }
}
