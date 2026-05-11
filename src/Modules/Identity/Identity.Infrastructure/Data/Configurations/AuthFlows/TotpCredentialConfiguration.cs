using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="TotpCredential"/>.</summary>
internal sealed class TotpCredentialConfiguration : IEntityTypeConfiguration<TotpCredential>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TotpCredential> builder)
    {
        builder.ToTable("totp_credentials", "identity");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();

        // Secret stored as text (Phase 4 will wrap with envelope encryption).
        builder.Property(t => t.EncryptedSecret).HasMaxLength(256).IsRequired();

        builder.Property(t => t.IsConfirmed).IsRequired();
        builder.Property(t => t.ConfirmedAt);

        // Hashed recovery codes stored as a JSONB column (JSON string on the entity).
        builder.Property(t => t.HashedRecoveryCodesJson)
            .HasColumnName("hashed_recovery_codes")
            .HasColumnType("jsonb")
            .IsRequired();

        // HashedRecoveryCodes is a computed view — not mapped by EF Core.
        builder.Ignore(t => t.HashedRecoveryCodes);

        // One active TOTP credential per user.
        builder.HasIndex(t => t.UserId).IsUnique();
    }
}
