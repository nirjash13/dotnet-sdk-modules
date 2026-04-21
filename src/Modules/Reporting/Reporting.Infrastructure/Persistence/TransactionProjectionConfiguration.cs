using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Application.Persistence;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for <see cref="TransactionProjection"/>.
/// </summary>
internal sealed class TransactionProjectionConfiguration
    : IEntityTypeConfiguration<TransactionProjection>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransactionProjection> builder)
    {
        builder.ToTable("transaction_projections", "reporting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.SourceMessageId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.AccountId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(19,4)")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Business-level idempotency guard.
        // Prevents duplicate projection rows for the same (tenant, source message).
        // MT InboxState is the first line of defence; this index is the second.
        builder.HasIndex(x => new { x.TenantId, x.SourceMessageId })
            .IsUnique()
            .HasDatabaseName("ix_transaction_projections_tenant_source_message");

        // Composite index for tenant-scoped queries.
        builder.HasIndex(x => new { x.TenantId, x.Id })
            .HasDatabaseName("IX_transaction_projections_TenantId_Id");
    }
}
