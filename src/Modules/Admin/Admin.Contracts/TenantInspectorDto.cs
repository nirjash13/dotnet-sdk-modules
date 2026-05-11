using System;
using System.Collections.Generic;

namespace Admin.Contracts;

/// <summary>
/// Full tenant inspection payload returned by the admin inspector endpoint.
/// Aggregates data from Identity, Billing, and Audit modules.
/// </summary>
public sealed class TenantInspectorDto
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the lifecycle status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets when the tenant was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the number of active members.</summary>
    public int MemberCount { get; set; }

    /// <summary>Gets or sets the current subscription details, if any.</summary>
    public SubscriptionSummary? Subscription { get; set; }

    /// <summary>Gets or sets a summary of recent audit events.</summary>
    public IReadOnlyList<RecentAuditEntry> RecentAuditEvents { get; set; } =
        Array.Empty<RecentAuditEntry>();

    /// <summary>Gets or sets support metadata for this tenant.</summary>
    public SupportMetadata Support { get; set; } = new SupportMetadata();

    /// <summary>Subscription summary nested type.</summary>
    public sealed class SubscriptionSummary
    {
        /// <summary>Gets or sets the subscription identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the plan identifier.</summary>
        public Guid PlanId { get; set; }

        /// <summary>Gets or sets the subscription status.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Gets or sets the provider subscription identifier.</summary>
        public string? ProviderSubscriptionId { get; set; }

        /// <summary>Gets or sets when the subscription started.</summary>
        public DateTimeOffset StartedAt { get; set; }

        /// <summary>Gets or sets the trial end date, if applicable.</summary>
        public DateTimeOffset? TrialEndsAt { get; set; }
    }

    /// <summary>Lightweight audit entry summary nested type.</summary>
    public sealed class RecentAuditEntry
    {
        /// <summary>Gets or sets the action verb.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Gets or sets the actor identifier.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Gets or sets when the event occurred.</summary>
        public DateTimeOffset Timestamp { get; set; }
    }

    /// <summary>Support metadata nested type.</summary>
    public sealed class SupportMetadata
    {
        /// <summary>Gets or sets the last admin action timestamp.</summary>
        public DateTimeOffset? LastAdminActionAt { get; set; }

        /// <summary>Gets or sets the last admin actor identifier.</summary>
        public string? LastAdminActorId { get; set; }

        /// <summary>Gets or sets the total number of admin actions taken on this tenant.</summary>
        public int TotalAdminActions { get; set; }
    }
}
