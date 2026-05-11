using Microsoft.EntityFrameworkCore;

namespace Foo.Infrastructure.Persistence;

/// <summary>Foo module database context.</summary>
public sealed class FooDbContext(DbContextOptions<FooDbContext> options)
    : DbContext(options)
{
    // TODO: add DbSet<> properties

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("foo");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FooDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
