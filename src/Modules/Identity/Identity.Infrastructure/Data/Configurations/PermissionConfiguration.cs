using Identity.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>EF Core entity type configuration for <see cref="Permission"/>.</summary>
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", "identity");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Resource).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Action).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Scope).HasMaxLength(50).IsRequired();

        // Computed column backed by ignored property — use a separate index column for Key.
        // Key is a computed C# property; store Resource+Action+Scope for querying.
        builder.HasIndex(p => new { p.Resource, p.Action, p.Scope }).IsUnique();

        // Ignore the computed Key property — it is not a database column.
        builder.Ignore(p => p.Key);
    }
}
