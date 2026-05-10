using System.Collections.Generic;

namespace Identity.Application.Authorization;

/// <summary>
/// Provides the built-in permission definitions for the Identity module.
/// These permissions cover organizations, members, invitations, and RBAC administration.
/// </summary>
/// <remarks>
/// Built-in role grant matrix (enforced by <c>RolePermissionSeeder</c> in Infrastructure):
/// <list type="table">
///   <listheader><term>Permission key</term><term>Owner</term><term>Admin</term><term>Member</term><term>ReadOnly</term></listheader>
///   <item><term>organizations.read:any</term><term>✓</term><term>✓</term><term>✓</term><term>✓</term></item>
///   <item><term>organizations.write:any</term><term>✓</term><term>✓</term><term></term><term></term></item>
///   <item><term>organizations.delete:any</term><term>✓</term><term></term><term></term><term></term></item>
///   <item><term>members.read:any</term><term>✓</term><term>✓</term><term>✓</term><term>✓</term></item>
///   <item><term>members.invite:any</term><term>✓</term><term>✓</term><term></term><term></term></item>
///   <item><term>members.remove:any</term><term>✓</term><term>✓</term><term></term><term></term></item>
///   <item><term>members.role.change:any</term><term>✓</term><term>✓</term><term></term><term></term></item>
///   <item><term>billing.read:any</term><term>✓</term><term>✓</term><term></term><term>✓</term></item>
///   <item><term>billing.write:any</term><term>✓</term><term>✓</term><term></term><term></term></item>
///   <item><term>billing.delete:any</term><term>✓</term><term></term><term></term><term></term></item>
/// </list>
/// </remarks>
public sealed class BuiltinPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public IEnumerable<PermissionDefinition> Define()
    {
        // Organizations
        yield return new PermissionDefinition("organizations", "read", "any", "View organization details.");
        yield return new PermissionDefinition("organizations", "write", "any", "Create or update organizations.");
        yield return new PermissionDefinition("organizations", "delete", "any", "Archive or delete organizations.");

        // Members
        yield return new PermissionDefinition("members", "read", "any", "View organization members.");
        yield return new PermissionDefinition("members", "invite", "any", "Invite new members to the organization.");
        yield return new PermissionDefinition("members", "remove", "any", "Remove members from the organization.");
        yield return new PermissionDefinition("members", "role.change", "any", "Change a member's role.");

        // Billing (deferred — Phase 4)
        yield return new PermissionDefinition("billing", "read", "any", "View billing information.");
        yield return new PermissionDefinition("billing", "write", "any", "Manage subscriptions and payment methods.");
        yield return new PermissionDefinition("billing", "delete", "any", "Cancel subscriptions.");
    }
}
