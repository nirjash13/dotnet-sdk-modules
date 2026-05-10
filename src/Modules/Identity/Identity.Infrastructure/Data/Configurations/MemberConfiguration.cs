using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>EF Core entity type configuration for <see cref="Member"/>.</summary>
internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("organization_members", "identity");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.OrganizationId).IsRequired();
        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.RoleId).IsRequired();
        builder.Property(m => m.Status).IsRequired();
        builder.Property(m => m.JoinedAt).IsRequired();
        builder.Property(m => m.InvitedById);

        // Prevent duplicate active memberships for the same user in the same org.
        builder.HasIndex(m => new { m.OrganizationId, m.UserId, m.Status });
    }
}
