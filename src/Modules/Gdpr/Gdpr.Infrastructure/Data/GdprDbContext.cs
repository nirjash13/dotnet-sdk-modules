using Gdpr.Contracts;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Gdpr.Infrastructure.Data;

/// <summary>EF Core DbContext for the GDPR bounded context.</summary>
/// <remarks>
/// Extends <see cref="SaasBuilderDbContext"/> so that tenant-scoped entities
/// (<see cref="GdprConsent"/>, <see cref="GdprErasureRequest"/>) automatically receive
/// a global query filter restricted to the current tenant context. This prevents a GDPR admin
/// from one tenant reading or cancelling another tenant's erasure requests (C-7 root cause).
/// </remarks>
public sealed class GdprDbContext : SaasBuilderDbContext
{
    /// <summary>Initializes a new instance of <see cref="GdprDbContext"/>.</summary>
    public GdprDbContext(
        DbContextOptions<GdprDbContext> options,
        ITenantContextAccessor tenantContextAccessor)
        : base(options, tenantContextAccessor)
    {
    }

    internal DbSet<GdprConsent> Consents => Set<GdprConsent>();

    internal DbSet<GdprErasureRequest> ErasureRequests => Set<GdprErasureRequest>();

    internal DbSet<GdprSubProcessor> SubProcessors => Set<GdprSubProcessor>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // base.OnModelCreating applies tenant global query filters for ITenantScoped entities.
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("gdpr");

        modelBuilder.Entity<GdprConsent>(e =>
        {
            e.ToTable("gdpr_consents");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedOnAdd();
            e.Property(c => c.ConsentKey).HasMaxLength(128).IsRequired();
            e.Property(c => c.Version).HasMaxLength(32).IsRequired();
            e.HasIndex(c => new { c.TenantId, c.UserId, c.ConsentKey, c.Timestamp });
        });

        modelBuilder.Entity<GdprErasureRequest>(e =>
        {
            e.ToTable("gdpr_erasure_requests");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedOnAdd();
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(r => new { r.Status, r.GraceEndsAt });
        });

        modelBuilder.Entity<GdprSubProcessor>(e =>
        {
            e.ToTable("gdpr_subprocessors");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedOnAdd();
            e.Property(s => s.Name).HasMaxLength(256).IsRequired();
            e.Property(s => s.Country).HasMaxLength(64).IsRequired();
            e.Property(s => s.Purpose).HasMaxLength(512).IsRequired();
            e.Property(s => s.DataTypes).HasMaxLength(512).IsRequired();
            e.Property(s => s.Website).HasMaxLength(256).IsRequired();
        });
    }
}
