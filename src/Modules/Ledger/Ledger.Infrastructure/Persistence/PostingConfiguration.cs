using Ledger.Domain.Entities;
using Ledger.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for the <see cref="Posting"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// <b>Money as owned type:</b> EF Core maps the <see cref="Money"/> value object as two
/// columns: <c>amount numeric(19,4)</c> and <c>currency char(3)</c>. This avoids a JSON
/// column and keeps the data relational and indexable.
/// </para>
/// <para>
/// <b>Idempotency index:</b> A partial unique index on
/// <c>(tenant_id, idempotency_key) WHERE idempotency_key IS NOT NULL</c> ensures that
/// duplicate <c>PostTransactionCommand</c> deliveries with the same idempotency key
/// produce exactly one posting row. The EF <c>HasFilter</c> clause is Postgres-specific.
/// </para>
/// </remarks>
internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        builder.ToTable("postings", "ledger");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.AccountId)
            .IsRequired();

        builder.Property(p => p.OccurredAt)
            .IsRequired();

        builder.Property(p => p.Memo)
            .HasMaxLength(500);

        builder.Property(p => p.IdempotencyKey);

        // Map Money value object as two columns using OwnsOne.
        // EF Core splits the owned entity's properties into the owner table.
        builder.OwnsOne(p => p.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(19,4)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasColumnType("char(3)")
                .IsRequired();
        });

        // Partial unique index for idempotency — only when idempotency_key is not null.
        // Postgres syntax: WHERE "IdempotencyKey" IS NOT NULL
        builder.HasIndex(p => new { p.TenantId, p.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL")
            .HasDatabaseName("ix_postings_tenant_idempotency_key");
    }
}
