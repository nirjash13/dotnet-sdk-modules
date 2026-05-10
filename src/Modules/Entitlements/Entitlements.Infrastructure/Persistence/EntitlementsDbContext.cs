using Entitlements.Domain;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Entitlements.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Entitlements bounded context.
/// Stores tenant-level entitlement overrides (sales-driven exceptions).
/// Edition-level entitlement grants are managed by the Billing module.
/// </summary>
public sealed class EntitlementsDbContext(
    DbContextOptions<EntitlementsDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the entitlement grants set (tenant-level overrides + edition grants).</summary>
    public DbSet<EntitlementGrant> EntitlementGrants => Set<EntitlementGrant>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("entitlements");

        modelBuilder.Entity<EntitlementGrant>(builder =>
        {
            builder.ToTable("entitlement_grants");

            builder.HasKey(g => g.Id);

            builder.Property(g => g.Id)
                .HasColumnName("id")
                .UseIdentityAlwaysColumn();

            builder.Property(g => g.TenantId)
                .HasColumnName("tenant_id");

            builder.Property(g => g.EditionId)
                .HasColumnName("edition_id");

            builder.Property(g => g.Key)
                .HasColumnName("key")
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(g => g.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(g => g.BoolValue)
                .HasColumnName("bool_value");

            builder.Property(g => g.NumericLimit)
                .HasColumnName("numeric_limit");

            builder.Property(g => g.StringValue)
                .HasColumnName("string_value")
                .HasMaxLength(1024);

            // Index for quick lookup of effective grants per tenant+edition.
            builder.HasIndex(g => new { g.TenantId, g.Key })
                .HasDatabaseName("ix_entitlement_grants_tenant_key");

            builder.HasIndex(g => new { g.EditionId, g.Key })
                .HasDatabaseName("ix_entitlement_grants_edition_key");
        });
    }
}
