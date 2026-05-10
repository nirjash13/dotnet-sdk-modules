using Billing.Domain.Entities;
using Billing.Infrastructure.Persistence.Configurations;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Billing bounded context.
/// Inherits <see cref="SaasBuilderDbContext"/> for automatic tenant query filters.
/// </summary>
public sealed class BillingDbContext(
    DbContextOptions<BillingDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the subscription aggregate root set.</summary>
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    /// <summary>Gets the product catalog set.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Gets the price set.</summary>
    public DbSet<Price> Prices => Set<Price>();

    /// <summary>Gets the edition set.</summary>
    public DbSet<Edition> Editions => Set<Edition>();

    /// <summary>Gets the plan set.</summary>
    public DbSet<Plan> Plans => Set<Plan>();

    /// <summary>Gets the processed webhook event set for idempotency deduplication.</summary>
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("billing");

        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookEventConfiguration());
        modelBuilder.ApplyConfiguration(new EditionConfiguration());
        modelBuilder.ApplyConfiguration(new EntitlementGrantConfiguration());

        // Apply MassTransit outbox schema for transactional outbox support.
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
    }
}
