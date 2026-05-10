namespace Identity.Domain.Organizations;

/// <summary>
/// Lifecycle status for an <see cref="Organization"/>.
/// </summary>
public enum OrganizationStatus
{
    /// <summary>The organization is active and can perform all operations.</summary>
    Active = 0,

    /// <summary>The organization has been suspended; write operations are blocked.</summary>
    Suspended = 1,

    /// <summary>The organization is archived; read-only access only.</summary>
    Archived = 2,
}
