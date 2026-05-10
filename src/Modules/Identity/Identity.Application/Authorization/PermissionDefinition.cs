namespace Identity.Application.Authorization;

/// <summary>
/// Describes a single permission that a module contributes to the permission registry.
/// </summary>
/// <param name="Resource">The resource category (e.g., "organizations").</param>
/// <param name="Action">The action within the resource (e.g., "member.invite").</param>
/// <param name="Scope">The scope modifier (default "any").</param>
/// <param name="Description">A human-readable description for admin UIs.</param>
public sealed record PermissionDefinition(
    string Resource,
    string Action,
    string Scope,
    string Description)
{
    /// <summary>Gets the canonical permission key: "{Resource}.{Action}:{Scope}".</summary>
    public string Key => $"{Resource}.{Action}:{Scope}";
}
