using System;

namespace Registration.Contracts;

/// <summary>
/// Terminal integration event published by the saga when provisioning fails and
/// all compensation steps have been attempted.
/// </summary>
public class RegistrationFailed
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier that failed to provision.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the reason for the failure.</summary>
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the failure was recorded.</summary>
    public DateTimeOffset FailedAt { get; set; }
}
