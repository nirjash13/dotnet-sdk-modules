using System;

namespace Admin.Contracts;

/// <summary>
/// Lightweight tenant row returned by the directory listing endpoint.
/// </summary>
public sealed class TenantSummaryDto
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant slug (URL-safe identifier).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the lifecycle status (e.g., "active", "suspended", "deleted").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets when the tenant was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the current subscription plan name, if any.</summary>
    public string? PlanName { get; set; }
}
