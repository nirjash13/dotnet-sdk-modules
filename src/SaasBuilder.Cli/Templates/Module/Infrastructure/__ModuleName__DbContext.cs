using Microsoft.EntityFrameworkCore;

namespace __ModuleName__.Infrastructure.Persistence;

/// <summary>__ModuleName__ module database context. Add DbSet properties here.</summary>
public sealed class __ModuleName__DbContext(DbContextOptions<__ModuleName__DbContext> options)
    : DbContext(options)
{
    // TODO: add DbSet<> properties

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("__module_schema__");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(__ModuleName__DbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
