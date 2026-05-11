using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="EmailVerificationToken"/>.</summary>
internal sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("email_verification_tokens", "identity");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);

        // Fast lookup by hash — unique to prevent duplicate active tokens.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Efficient cleanup of expired/used tokens.
        builder.HasIndex(t => new { t.UserId, t.UsedAt });
    }
}
