using System;

namespace Registration.Contracts;

/// <summary>Command sent by the saga to the Ledger module to initialize the tenant's default account.</summary>
public class InitLedger
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant being provisioned.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency code for the account.</summary>
    public string Currency { get; set; } = string.Empty;
}

/// <summary>Response sent by the Ledger consumer when ledger initialization succeeds.</summary>
public class LedgerInitialized
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the newly created account identifier.</summary>
    public Guid AccountId { get; set; }
}

/// <summary>Compensation command sent by the saga to the Ledger module to roll back account creation.</summary>
public class RollbackLedgerInit
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the account identifier to remove.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the tenant identifier (for idempotent guard).</summary>
    public Guid TenantId { get; set; }
}

/// <summary>Response sent by the Ledger consumer when ledger rollback succeeds.</summary>
public class LedgerRolledBack
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the rolled-back account identifier.</summary>
    public Guid AccountId { get; set; }
}
