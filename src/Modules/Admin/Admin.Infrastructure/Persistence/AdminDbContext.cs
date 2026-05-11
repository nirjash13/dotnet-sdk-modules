using Admin.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Admin.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Admin bounded context.
/// Stores admin action audit records and pending approval requests.
/// Both tables are append-oriented — writes are INSERT-only in production.
/// </summary>
public sealed class AdminDbContext(
    DbContextOptions<AdminDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the admin action audit entries.</summary>
    public DbSet<AdminActionAuditEntry> AdminActionAuditEntries => Set<AdminActionAuditEntry>();

    /// <summary>Gets the pending admin actions awaiting approval.</summary>
    public DbSet<PendingAdminActionEntity> PendingAdminActions => Set<PendingAdminActionEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("admin");

        modelBuilder.Entity<AdminActionAuditEntry>(e =>
        {
            e.ToTable("admin_action_audit");
            e.HasKey(a => a.Id);
            e.Property(a => a.ActorId).HasMaxLength(256).IsRequired();
            e.Property(a => a.Action).HasMaxLength(256).IsRequired();
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.Property(a => a.UserAgent).HasMaxLength(512);
            e.Property(a => a.PayloadJson).HasColumnType("text");
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.ActorId);
            e.HasIndex(a => a.TargetTenantId);
        });

        modelBuilder.Entity<PendingAdminActionEntity>(e =>
        {
            e.ToTable("admin_approvals");
            e.HasKey(a => a.Id);
            e.Property(a => a.RequestorId).HasMaxLength(256).IsRequired();
            e.Property(a => a.Action).HasMaxLength(256).IsRequired();
            e.Property(a => a.ApproverId).HasMaxLength(256);
            e.Property(a => a.DenialReason).HasMaxLength(1024);
            e.Property(a => a.PayloadJson).HasColumnType("text");
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(a => a.Sensitivity).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(a => a.Status);
            e.HasIndex(a => a.RequestorId);
        });
    }
}
