using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;
using Search.Infrastructure.Entities;

namespace Search.Infrastructure.Persistence;

/// <summary>
/// EF Core bounded-context for the Search module.
/// Contains the generic <c>search_documents</c> table used by the Postgres FTS client.
/// </summary>
public sealed class SearchDbContext(
    DbContextOptions<SearchDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the generic search documents set.</summary>
    public DbSet<SearchDocument> SearchDocuments => Set<SearchDocument>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("search");

        modelBuilder.Entity<SearchDocument>(e =>
        {
            e.ToTable("search_documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.IndexName).HasMaxLength(128).IsRequired();
            e.Property(d => d.DocumentId).HasMaxLength(256).IsRequired();
            e.Property(d => d.ContentJson).HasColumnType("jsonb").IsRequired();
            e.Property(d => d.SearchVector).HasColumnType("tsvector");
            e.HasIndex(d => new { d.IndexName, d.TenantId, d.DocumentId }).IsUnique();
            e.HasIndex(d => d.SearchVector).HasMethod("GIN");
        });
    }
}
