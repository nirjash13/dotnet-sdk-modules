using Chassis.Persistence;
using Chassis.SharedKernel.Tenancy;
using Ledger.Domain.Entities;
using Ledger.Domain.ValueObjects;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Ledger bounded context.
/// Inherits <see cref="ChassisDbContext"/> for automatic tenant query filters and
/// the <c>TenantCommandInterceptor</c> that issues <c>SET LOCAL app.tenant_id</c>
/// before every command.
/// </summary>
public sealed class LedgerDbContext(
    DbContextOptions<LedgerDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : ChassisDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the account aggregate root set.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Gets the posting entity set.</summary>
    public DbSet<Posting> Postings => Set<Posting>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must call base first so tenant query filters are applied before entity configuration.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new PostingConfiguration());

        // Apply MassTransit outbox schema for transactional outbox support.
        // These tables are written atomically with EF Core transactions and drained
        // by the BusOutboxDeliveryService background worker.
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
    }
}
