using System;
using SaasBuilder.SharedKernel.Tenancy.Lifecycle;

namespace SaasBuilder.Tenancy.Lifecycle;

/// <summary>
/// Persistent record of a tenant's current lifecycle state.
/// Stored in the <c>saasbuilder.tenants</c> table.
/// </summary>
/// <remarks>
/// This is an infrastructure entity — it is not part of any domain aggregate.
/// Do not expose this class outside of the Infrastructure layer.
/// </remarks>
public sealed class TenantRecord
{
    /// <summary>Gets or sets the tenant identifier (primary key).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the current lifecycle status.</summary>
    public TenantStatus Status { get; set; }

    /// <summary>Gets or sets the slug (URL-friendly name) for the tenant.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name of the tenant.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when this tenant was created (provisioned).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the most recent status transition.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the tenant was suspended, or <see langword="null"/>
    /// when not suspended.
    /// </summary>
    public DateTimeOffset? SuspendedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the tenant was archived, or <see langword="null"/>
    /// when not archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the tenant will be hard-deleted, or <see langword="null"/>
    /// when no hard-delete is scheduled.
    /// </summary>
    public DateTimeOffset? ScheduledDeletionAt { get; set; }
}
