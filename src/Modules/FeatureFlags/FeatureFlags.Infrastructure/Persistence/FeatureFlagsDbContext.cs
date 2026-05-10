using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace FeatureFlags.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the FeatureFlags bounded context.
/// Flags are global (not tenant-scoped); only <see cref="TenantFlagOverride"/> is per-tenant.
/// </summary>
public sealed class FeatureFlagsDbContext(
    DbContextOptions<FeatureFlagsDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the feature flag definitions.</summary>
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    /// <summary>Gets the targeting rules.</summary>
    public DbSet<TargetingRule> TargetingRules => Set<TargetingRule>();

    /// <summary>Gets the tenant-level flag overrides.</summary>
    public DbSet<TenantFlagOverride> TenantFlagOverrides => Set<TenantFlagOverride>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("feature_flags");

        modelBuilder.Entity<FeatureFlag>(builder =>
        {
            builder.ToTable("feature_flags");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Key).HasMaxLength(255).IsRequired();
            builder.Property(f => f.Description).HasMaxLength(1024);
            builder.HasIndex(f => f.Key).IsUnique().HasDatabaseName("ux_feature_flags_key");

            builder.HasMany(f => f.TargetingRules)
                .WithOne()
                .HasForeignKey(r => r.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(f => f.TenantOverrides)
                .WithOne()
                .HasForeignKey(o => o.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TargetingRule>(builder =>
        {
            builder.ToTable("targeting_rules");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.AttributeKey).HasMaxLength(255).IsRequired();
            builder.Property(r => r.AttributeValue).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<TenantFlagOverride>(builder =>
        {
            builder.ToTable("tenant_flag_overrides");
            builder.HasKey(o => o.Id);

            // One override per tenant per flag.
            builder.HasIndex(o => new { o.TenantId, o.FeatureFlagId })
                .IsUnique()
                .HasDatabaseName("ux_tenant_flag_overrides_tenant_flag");
        });
    }
}
