using System;
using Ai.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ai.Infrastructure.VectorStores;

/// <summary>
/// EF Core DbContext for the AI module's persistence concerns:
/// vector documents and LLM usage records.
/// </summary>
public sealed class AiDbContext : DbContext
{
    /// <summary>Initializes a new instance of <see cref="AiDbContext"/>.</summary>
    public AiDbContext(DbContextOptions<AiDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the vector document store.</summary>
    public DbSet<VectorDocument> VectorDocuments => Set<VectorDocument>();

    /// <summary>Gets the append-only LLM usage records.</summary>
    public DbSet<LlmUsageRecord> LlmUsageRecords => Set<LlmUsageRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<VectorDocument>(entity =>
        {
            entity.ToTable("vector_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.EmbeddingJson).IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.MetadataJson).IsRequired().HasDefaultValue("{}");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.TenantId).HasDatabaseName("ix_vector_documents_tenant_id");
        });

        modelBuilder.Entity<LlmUsageRecord>(entity =>
        {
            entity.ToTable("llm_usage_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Model).IsRequired().HasMaxLength(128);
            entity.Property(e => e.RequestId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CostUsd).HasPrecision(18, 8);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt })
                .HasDatabaseName("ix_llm_usage_records_tenant_created");
        });
    }
}
