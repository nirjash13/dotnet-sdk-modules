using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations.AuthFlows;

/// <summary>EF Core entity type configuration for <see cref="UserTombstone"/>.</summary>
internal sealed class UserTombstoneConfiguration : IEntityTypeConfiguration<UserTombstone>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserTombstone> builder)
    {
        builder.ToTable("user_tombstones", "identity");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.OriginalUserId).IsRequired();
        builder.Property(t => t.DeletedAt).IsRequired();
        builder.Property(t => t.HardDeletedAt).IsRequired();

        // Index for audit lookups by original user id.
        builder.HasIndex(t => t.OriginalUserId);
    }
}
