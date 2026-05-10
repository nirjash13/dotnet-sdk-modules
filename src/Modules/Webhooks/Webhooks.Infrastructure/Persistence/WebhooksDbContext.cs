using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;
using Webhooks.Domain.Entities;
using Webhooks.Infrastructure.Entities;

namespace Webhooks.Infrastructure.Persistence;

/// <summary>EF Core bounded-context for the Webhooks module.</summary>
public sealed class WebhooksDbContext(
    DbContextOptions<WebhooksDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the webhook endpoints set.</summary>
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    /// <summary>Gets the webhook events set.</summary>
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    /// <summary>Gets the webhook delivery attempts set.</summary>
    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("webhooks");

        modelBuilder.Entity<WebhookEndpoint>(e =>
        {
            e.ToTable("webhook_endpoints");
            e.HasKey(w => w.Id);
            e.Property(w => w.Url).HasMaxLength(2048).IsRequired();
            e.Property(w => w.Description).HasMaxLength(512);
            e.Property(w => w.SecretHashedCurrent).HasMaxLength(512).IsRequired();
            e.Property(w => w.SecretHashedPrevious).HasMaxLength(512);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(32);

            // EventTypes stored as JSON array column.
            e.Property(w => w.EventTypes)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                        ?? new System.Collections.Generic.List<string>());

            e.HasIndex(w => new { w.TenantId, w.Status });
        });

        modelBuilder.Entity<WebhookEvent>(e =>
        {
            e.ToTable("webhook_events");
            e.HasKey(w => w.Id);
            e.Property(w => w.EventType).HasMaxLength(256).IsRequired();
            e.HasIndex(w => new { w.TenantId, w.EventType, w.CreatedAt });
        });

        modelBuilder.Entity<WebhookDeliveryAttempt>(e =>
        {
            e.ToTable("webhook_delivery_attempts");
            e.HasKey(d => d.Id);
            e.Property(d => d.ResponseBody).HasMaxLength(1024);
            e.HasIndex(d => new { d.EndpointId, d.AttemptedAt });
            e.HasIndex(d => d.EventId);
        });
    }
}
