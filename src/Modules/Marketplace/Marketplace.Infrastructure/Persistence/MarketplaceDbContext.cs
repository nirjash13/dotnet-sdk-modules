using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Persistence;

/// <summary>EF Core DbContext for the Marketplace module.</summary>
public sealed class MarketplaceDbContext : DbContext
{
    /// <summary>Initializes a new instance of <see cref="MarketplaceDbContext"/>.</summary>
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the marketplace app catalogue.</summary>
    public DbSet<MarketplaceApp> Apps => Set<MarketplaceApp>();

    /// <summary>Gets tenant app installations.</summary>
    public DbSet<AppInstallation> Installations => Set<AppInstallation>();

    /// <summary>Gets scope definitions for each app.</summary>
    public DbSet<AppScope> AppScopes => Set<AppScope>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MarketplaceApp>(entity =>
        {
            entity.ToTable("marketplace_apps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("uix_marketplace_apps_slug");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Vendor).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.ManifestJson).IsRequired().HasDefaultValue("{}");
            entity.Property(e => e.IsListed).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<AppInstallation>(entity =>
        {
            entity.ToTable("marketplace_app_installations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.AppId).IsRequired();
            entity.Property(e => e.OAuthClientId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.GrantedScopesJson).IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.InstalledAt).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.HasIndex(e => new { e.TenantId, e.AppId })
                .HasDatabaseName("ix_marketplace_installations_tenant_app");
        });

        modelBuilder.Entity<AppScope>(entity =>
        {
            entity.ToTable("marketplace_app_scopes");
            entity.HasKey(e => new { e.AppId, e.Scope });
            entity.Property(e => e.Scope).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Sensitivity).IsRequired();
        });
    }
}
