using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="WebhookEvent"/>.
/// </summary>
internal sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events");

        builder.HasKey(e => e.RowId);

        builder.Property(e => e.RowId)
            .HasColumnName("row_id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();

        // The unique constraint on idempotency_key is the DB-level dedup guard.
        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_webhook_events_idempotency_key");
    }
}
