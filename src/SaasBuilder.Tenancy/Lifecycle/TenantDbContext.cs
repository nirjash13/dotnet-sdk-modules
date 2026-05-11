using Microsoft.EntityFrameworkCore;

namespace SaasBuilder.Tenancy.Lifecycle;

/// <summary>
/// EF Core context for the <c>saasbuilder.tenants</c> table.
/// This context is intentionally narrow — it owns only the tenant lifecycle record.
/// </summary>
public sealed class TenantDbContext : DbContext
{
    /// <summary>Initializes the context with the provided options.</summary>
    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the tenants set.</summary>
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantRecord>(entity =>
        {
            entity.ToTable("tenants", "saasbuilder");
            entity.HasKey(t => t.TenantId);
            entity.Property(t => t.TenantId).ValueGeneratedNever();
            entity.Property(t => t.Slug).HasMaxLength(128).IsRequired();
            entity.Property(t => t.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(t => t.Status).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.Property(t => t.UpdatedAt).IsRequired();
            entity.HasIndex(t => t.Slug).IsUnique();
        });
    }
}
