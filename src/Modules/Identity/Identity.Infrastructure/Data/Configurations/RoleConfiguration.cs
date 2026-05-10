using Identity.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>EF Core entity type configuration for <see cref="Role"/>.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "identity");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrganizationId);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.IsSystem).IsRequired();

        // System role names are globally unique; org-scoped names are unique per org.
        builder.HasIndex(r => new { r.OrganizationId, r.Name }).IsUnique();
    }
}
