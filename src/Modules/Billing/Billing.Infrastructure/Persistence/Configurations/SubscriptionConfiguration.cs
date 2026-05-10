using Billing.Domain.Entities;
using Billing.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="Subscription"/>.
/// </summary>
internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(s => s.ProviderSubscriptionId)
            .HasColumnName("provider_subscription_id")
            .HasMaxLength(255);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(s => s.CanceledAt)
            .HasColumnName("canceled_at");

        builder.Property(s => s.PausedAt)
            .HasColumnName("paused_at");

        builder.Property(s => s.TrialEndsAt)
            .HasColumnName("trial_ends_at");

        // One active subscription per tenant (enforced at DB level).
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_subscriptions_tenant_id");

        // For provider-side lookups during webhook processing.
        builder.HasIndex(s => s.ProviderSubscriptionId)
            .HasDatabaseName("ix_subscriptions_provider_subscription_id")
            .IsUnique()
            .HasFilter("provider_subscription_id IS NOT NULL");
    }
}
