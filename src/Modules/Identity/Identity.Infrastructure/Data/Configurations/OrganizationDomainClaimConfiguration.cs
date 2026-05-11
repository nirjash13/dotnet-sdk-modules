using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>EF Core entity type configuration for <see cref="OrganizationDomainClaim"/>.</summary>
internal sealed class OrganizationDomainClaimConfiguration : IEntityTypeConfiguration<OrganizationDomainClaim>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganizationDomainClaim> builder)
    {
        builder.ToTable("organization_domain_claims", "identity");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.OrganizationId).IsRequired();
        builder.Property(c => c.Domain).HasMaxLength(253).IsRequired();
        builder.Property(c => c.VerificationToken).HasMaxLength(64);
        builder.Property(c => c.VerifiedAt);
        builder.Property(c => c.CreatedAt).IsRequired();

        // Domain must be globally unique across all orgs.
        builder.HasIndex(c => c.Domain).IsUnique();
    }
}
