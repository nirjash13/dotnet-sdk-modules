using System;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Persistence;
using Chassis.SharedKernel.Tenancy;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Abstractions;
using Reporting.Application.Persistence;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Reporting bounded context.
/// Inherits <see cref="ChassisDbContext"/> for automatic tenant query filters and
/// <c>TenantCommandInterceptor</c> RLS enforcement.
/// Also implements <see cref="IReportingDbContext"/> so Application-layer consumers
/// can interact with it without a direct EF Core dependency.
/// </summary>
public sealed class ReportingDbContext(
    DbContextOptions<ReportingDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : ChassisDbContext(options, tenantContextAccessor), IReportingDbContext
{
    /// <summary>Gets the transaction projections set.</summary>
    public DbSet<TransactionProjection> TransactionProjections => Set<TransactionProjection>();

    /// <inheritdoc />
    public async Task InsertIfNotExistsAsync(
        TransactionProjection projection,
        CancellationToken ct = default)
    {
        // Check-then-insert guarded by the business-level unique index on (TenantId, SourceMessageId).
        // The index ensures at-most-once semantics even if two concurrent inserts race past the check.
        bool exists = await ExistsAsync(projection.TenantId, projection.SourceMessageId, ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            TransactionProjections.Add(projection);
            await SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid sourceMessageId,
        CancellationToken ct = default)
    {
        return TransactionProjections
            .AsNoTracking()
            .AnyAsync(
                p => p.TenantId == tenantId && p.SourceMessageId == sourceMessageId,
                ct);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must call base first so tenant query filters are applied before entity configuration.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TransactionProjectionConfiguration());

        // Apply MassTransit inbox/outbox schema for idempotent consumer protection.
        // These tables are created in the reporting schema.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
