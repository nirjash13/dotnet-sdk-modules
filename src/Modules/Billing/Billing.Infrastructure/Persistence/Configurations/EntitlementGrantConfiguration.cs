using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for the edition-scoped <see cref="EntitlementGrant"/>.
/// Uses a composite primary key of (EditionId, Key).
/// </summary>
internal sealed class EntitlementGrantConfiguration : IEntityTypeConfiguration<EntitlementGrant>
{
    public void Configure(EntityTypeBuilder<EntitlementGrant> builder)
    {
        builder.ToTable("edition_entitlement_grants");

        builder.HasKey(g => new { g.EditionId, g.Key });

        builder.Property(g => g.EditionId)
            .HasColumnName("edition_id")
            .IsRequired();

        builder.Property(g => g.Key)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.BoolValue)
            .HasColumnName("bool_value");

        builder.Property(g => g.NumericLimit)
            .HasColumnName("numeric_limit");

        builder.Property(g => g.StringValue)
            .HasColumnName("string_value")
            .HasMaxLength(500);
    }
}
