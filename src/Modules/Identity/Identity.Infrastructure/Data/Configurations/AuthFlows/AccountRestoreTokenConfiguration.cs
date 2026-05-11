using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="AccountRestoreToken"/>.</summary>
internal sealed class AccountRestoreTokenConfiguration : IEntityTypeConfiguration<AccountRestoreToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountRestoreToken> builder)
    {
        builder.ToTable("account_restore_tokens", "identity");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.UsedAt });
    }
}
