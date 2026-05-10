using System.Threading;
using System.Threading.Tasks;

namespace SaasBuilder.Persistence.Migrations;

/// <summary>
/// Runs pending EF Core migrations for all registered <see cref="IDbContextMigrator"/>s
/// in dependency order, with a Postgres advisory lock to prevent concurrent runs across
/// multiple instances.
/// </summary>
/// <remarks>
/// Leader election is provided by <c>pg_try_advisory_lock(74239482)</c> — a deterministic
/// lock key (decimal encoding of the ASCII bytes "saas") that is held for the duration of
/// the migration run and released automatically when the session ends.
/// </remarks>
public interface IMigrationRunner
{
    /// <summary>
    /// Acquires the advisory lock, orders all registered migrators by their dependency graph,
    /// and applies pending migrations. Releases the lock when complete or on failure.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task RunPendingAsync(CancellationToken ct = default);
}
