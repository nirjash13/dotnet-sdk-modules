namespace Identity.Domain.Organizations;

/// <summary>
/// Lifecycle status for a <see cref="Member"/> within an <see cref="Organization"/>.
/// </summary>
public enum MemberStatus
{
    /// <summary>The member has been invited but has not yet accepted.</summary>
    Invited = 0,

    /// <summary>The member is active and can perform operations within the organization.</summary>
    Active = 1,

    /// <summary>The member has been suspended; access is blocked.</summary>
    Suspended = 2,

    /// <summary>The member has been removed from the organization.</summary>
    Removed = 3,
}
