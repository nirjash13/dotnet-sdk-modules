using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>EF Core entity type configuration for <see cref="Organization"/>.</summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", "identity");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.TenantId).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(100).IsRequired();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.BrandingJson).HasColumnType("jsonb");
        builder.Property(o => o.SettingsJson).HasColumnType("jsonb");
        builder.Property(o => o.Status).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt);

        // Slug is unique within a tenant.
        builder.HasIndex(o => new { o.TenantId, o.Slug }).IsUnique();

        // Members navigation — not loaded by default (no Include without explicit projection).
        builder.Metadata
            .FindNavigation(nameof(Organization.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // DomainClaims navigation — field-backed, not loaded by default.
        builder.HasMany(o => o.DomainClaims)
            .WithOne()
            .HasForeignKey(c => c.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Organization.DomainClaims))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
