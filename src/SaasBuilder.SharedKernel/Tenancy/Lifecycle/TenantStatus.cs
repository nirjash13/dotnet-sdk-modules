namespace SaasBuilder.SharedKernel.Tenancy.Lifecycle;

/// <summary>
/// State machine values for the tenant lifecycle.
/// Transitions: Provisioning → Active → Suspended → Active (un-suspend) or Archived → Deleted.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// Tenant is being provisioned (resources created, roles seeded, welcome email queued).
    /// Write operations are blocked. Reads are blocked.
    /// </summary>
    Provisioning = 0,

    /// <summary>Tenant is fully operational.</summary>
    Active = 1,

    /// <summary>
    /// Tenant is suspended (e.g., payment failure, policy violation).
    /// Write operations return 423 Locked. Read operations are permitted for billing queries.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Tenant data has been exported and the account is read-only.
    /// Hard-delete is scheduled after the retention period.
    /// </summary>
    Archived = 3,

    /// <summary>
    /// Tenant data has been hard-deleted after the retention period.
    /// This is a terminal state — no recovery is possible.
    /// </summary>
    Deleted = 4,
}
