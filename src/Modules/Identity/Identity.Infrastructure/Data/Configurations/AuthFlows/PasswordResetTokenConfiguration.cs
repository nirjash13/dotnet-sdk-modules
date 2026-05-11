using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="PasswordResetToken"/>.</summary>
internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens", "identity");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.UsedAt });
    }
}
