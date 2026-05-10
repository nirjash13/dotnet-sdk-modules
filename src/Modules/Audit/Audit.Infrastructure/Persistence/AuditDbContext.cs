using Microsoft.EntityFrameworkCore;
using Audit.Infrastructure.Entities;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Audit.Infrastructure.Persistence;

/// <summary>
/// EF Core bounded-context for the Audit module.
/// The <c>audit_entries</c> table is append-only by design.
/// Enforce this at the DB level: revoke UPDATE and DELETE privileges on the table
/// for the application role in production.
/// </summary>
public sealed class AuditDbContext(
    DbContextOptions<AuditDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the audit entries set.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_entries");
            e.HasKey(a => a.Id);
            e.Property(a => a.ActorId).HasMaxLength(256).IsRequired();
            e.Property(a => a.Action).HasMaxLength(256).IsRequired();
            e.Property(a => a.ResourceType).HasMaxLength(128).IsRequired();
            e.Property(a => a.ResourceId).HasMaxLength(256).IsRequired();
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.Property(a => a.UserAgent).HasMaxLength(512);
            e.Property(a => a.CorrelationId).HasMaxLength(128);
            e.Property(a => a.PrevHash).HasMaxLength(128);
            e.Property(a => a.Hash).HasMaxLength(128);
            e.HasIndex(a => new { a.TenantId, a.Timestamp });
            e.HasIndex(a => new { a.TenantId, a.ActorId });
            e.HasIndex(a => new { a.TenantId, a.ResourceType, a.ResourceId });
        });
    }
}
