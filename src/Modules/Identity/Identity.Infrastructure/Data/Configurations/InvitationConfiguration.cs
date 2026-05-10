using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>EF Core entity type configuration for <see cref="Invitation"/>.</summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("organization_invitations", "identity");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrganizationId).IsRequired();
        builder.Property(i => i.Email).HasMaxLength(320).IsRequired();
        builder.Property(i => i.RoleId).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.RedeemedAt);
        builder.Property(i => i.RevokedAt);
        builder.Property(i => i.CreatedById).IsRequired();

        // Index for token lookup (accept invitation endpoint).
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // Composite index: active invitations by org + email.
        builder.HasIndex(i => new { i.OrganizationId, i.Email });
    }
}
