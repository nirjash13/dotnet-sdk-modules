using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SaasBuilder.Persistence.Migrations;

/// <summary>
/// Applies pending EF Core migrations for a single bounded context.
/// Registered by each module so the <see cref="IMigrationRunner"/> can invoke all of them
/// in dependency order.
/// </summary>
public interface IDbContextMigrator
{
    /// <summary>
    /// Gets the bounded context types that this migrator depends on.
    /// The runner uses these dependencies to determine execution order.
    /// An empty collection means this migrator has no dependencies and may run first.
    /// </summary>
    IEnumerable<Type> DependsOn { get; }

    /// <summary>
    /// Applies all pending migrations for the associated <c>DbContext</c>.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task MigrateAsync(CancellationToken ct = default);
}
