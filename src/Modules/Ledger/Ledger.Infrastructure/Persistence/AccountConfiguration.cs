using Ledger.Domain.Entities;
using Ledger.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for the <see cref="Account"/> aggregate root.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", "ledger");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .IsRequired();

        builder.Property(a => a.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // A tenant may have many accounts; navigation from Posting to Account.
        // UsePropertyAccessMode(PropertyAccessMode.Field) tells EF Core to use the _postings
        // backing field when reading/writing the collection, so new Postings added via
        // Account.Post() are tracked correctly.
        builder.HasMany(a => a.Postings)
            .WithOne()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Postings)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_postings");

        // Index for tenant-scoped account lookups.
        builder.HasIndex(a => new { a.TenantId, a.Id });
    }
}
