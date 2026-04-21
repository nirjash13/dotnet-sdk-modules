using System;
using Chassis.SharedKernel.Abstractions;

namespace Identity.Contracts;

/// <summary>
/// Integration event published when a user is added to a tenant.
/// Consumed by downstream modules that need to react to membership changes
/// (e.g., Ledger provisioning, Reporting setup).
/// </summary>
/// <remarks>
/// Uses a class with read-only init properties (not a record with <c>required</c>) because
/// this type multi-targets <c>netstandard2.0</c> which does not have
/// <c>System.Runtime.CompilerServices.IsExternalInit</c>. See CHANGELOG_AI.md Phase 0 design note.
/// </remarks>
public sealed class TenantMembershipCreated : IIntegrationEvent
{
    /// <summary>Initializes all required fields.</summary>
    public TenantMembershipCreated(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid tenantId,
        Guid userId,
        Guid membershipId,
        string[] roles,
        bool isPrimary)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        TenantId = tenantId;
        UserId = userId;
        MembershipId = membershipId;
        Roles = roles ?? Array.Empty<string>();
        IsPrimary = isPrimary;
    }

    /// <summary>Gets the unique identifier of this event occurrence.</summary>
    public Guid EventId { get; }

    /// <summary>Gets the UTC timestamp when the membership was created.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Gets the tenant the user was added to.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the user who was added.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the membership identifier (links user to tenant).</summary>
    public Guid MembershipId { get; }

    /// <summary>Gets the roles assigned to the user within this tenant.</summary>
    public string[] Roles { get; }

    /// <summary>Gets a value indicating whether this is the user's primary tenant.</summary>
    public bool IsPrimary { get; }
}
