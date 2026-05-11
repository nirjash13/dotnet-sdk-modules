using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="ApiKey"/>.</summary>
internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys", "identity");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.UserId);
        builder.Property(k => k.OrganizationId);
        builder.Property(k => k.Name).HasMaxLength(200).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(20).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(128).IsRequired();
        builder.Property(k => k.ScopesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(k => k.LastUsedAt);
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.RevokedAt);

        // Fast lookup by hash — the primary auth hot path.
        builder.HasIndex(k => k.KeyHash).IsUnique();

        // List keys by user.
        builder.HasIndex(k => new { k.UserId, k.RevokedAt });

        // List keys by org.
        builder.HasIndex(k => new { k.OrganizationId, k.RevokedAt });
    }
}
