namespace Chassis.Host.Tenancy;

/// <summary>
/// Claim name constants shared by the JWT token issuer (Phase 2) and the middleware consumers.
/// </summary>
internal static class TenantClaims
{
    /// <summary>The tenant identifier claim — a UUID string.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>The authenticated user identifier claim (JWT <c>sub</c> equivalent for tenant members).</summary>
    public const string UserId = "user_id";

    /// <summary>The roles claim — a space-separated list or array of role names.</summary>
    public const string Roles = "roles";

    /// <summary>The membership identifier claim linking the user to their tenant-scoped membership record.</summary>
    public const string MembershipId = "membership_id";
}
