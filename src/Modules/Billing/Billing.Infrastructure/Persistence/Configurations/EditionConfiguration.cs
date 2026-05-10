using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="Edition"/> and its owned
/// <see cref="EntitlementGrant"/> collection.
/// </summary>
internal sealed class EditionConfiguration : IEntityTypeConfiguration<Edition>
{
    public void Configure(EntityTypeBuilder<Edition> builder)
    {
        builder.ToTable("editions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Key)
            .HasColumnName("key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(e => e.Key)
            .IsUnique()
            .HasDatabaseName("ix_editions_key");

        builder.HasMany(e => e.Entitlements)
            .WithOne()
            .HasForeignKey(g => g.EditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
