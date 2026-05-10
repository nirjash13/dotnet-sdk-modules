using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SaasBuilder.Persistence.Migrations;

/// <summary>
/// Default <see cref="IDbContextMigrator"/> that applies pending EF Core migrations
/// for a specific <typeparamref name="TContext"/> via <see cref="DatabaseFacade.MigrateAsync"/>.
/// </summary>
/// <typeparam name="TContext">The <see cref="DbContext"/> subclass to migrate.</typeparam>
public sealed class EfCoreDbContextMigrator<TContext> : IDbContextMigrator
    where TContext : DbContext
{
    private readonly TContext _context;

    /// <summary>
    /// Initializes the migrator with the given context.
    /// </summary>
    /// <param name="context">The EF Core context to migrate.</param>
    public EfCoreDbContextMigrator(TContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns an empty collection — callers that have dependencies should subclass this
    /// or compose it, overriding <see cref="DependsOn"/> to return the dependency types.
    /// </remarks>
    public IEnumerable<Type> DependsOn => Array.Empty<Type>();

    /// <inheritdoc />
    public Task MigrateAsync(CancellationToken ct = default)
        => _context.Database.MigrateAsync(ct);
}
