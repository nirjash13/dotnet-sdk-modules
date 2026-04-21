using System;

namespace Registration.Contracts;

/// <summary>Command sent by the saga to the Reporting module to bootstrap tenant reporting data.</summary>
public class ProvisionReporting
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant being provisioned.</summary>
    public Guid TenantId { get; set; }
}

/// <summary>Response sent by the Reporting consumer when provisioning succeeds.</summary>
public class ReportingProvisioned
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets a token identifying the reporting bootstrap record.</summary>
    public Guid ReportingId { get; set; }
}

/// <summary>Compensation command sent by the saga to the Reporting module to roll back provisioning.</summary>
public class UnprovisionReporting
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the reporting bootstrap identifier to remove.</summary>
    public Guid ReportingId { get; set; }

    /// <summary>Gets or sets the tenant identifier (for idempotent guard).</summary>
    public Guid TenantId { get; set; }
}
