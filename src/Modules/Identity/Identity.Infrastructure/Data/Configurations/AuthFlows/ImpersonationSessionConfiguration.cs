using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="ImpersonationSessionEntity"/>.</summary>
internal sealed class ImpersonationSessionConfiguration : IEntityTypeConfiguration<ImpersonationSessionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ImpersonationSessionEntity> builder)
    {
        builder.ToTable("impersonation_sessions", "identity");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AdminUserId).IsRequired();
        builder.Property(s => s.TargetUserId).IsRequired();
        builder.Property(s => s.Reason).HasMaxLength(1000).IsRequired();

        // Token stored for immediate invalidation on session end.
        builder.Property(s => s.ImpersonationToken).HasMaxLength(2048).IsRequired();

        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.EndedAt);

        // Audit lookup by admin.
        builder.HasIndex(s => new { s.AdminUserId, s.StartedAt });

        // Audit lookup by target.
        builder.HasIndex(s => new { s.TargetUserId, s.StartedAt });
    }
}
