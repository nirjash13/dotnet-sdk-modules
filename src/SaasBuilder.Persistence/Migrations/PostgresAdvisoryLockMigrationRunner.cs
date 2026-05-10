using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SaasBuilder.Persistence.Migrations;

/// <summary>
/// <see cref="IMigrationRunner"/> that acquires a Postgres session-level advisory lock
/// before running migrations, preventing concurrent runs across multiple app instances
/// during rolling deployments.
/// </summary>
/// <remarks>
/// Lock key: <c>pg_try_advisory_lock(74239482)</c> — decimal representation of
/// the ASCII bytes for "saas" (0x73 0x61 0x61 0x73 → 1937010035, truncated to 32-bit signed).
/// The lock is released automatically when the Npgsql connection is closed/disposed.
///
/// Migrators are ordered topologically by their <see cref="IDbContextMigrator.DependsOn"/>
/// declarations. Circular dependencies cause an <see cref="InvalidOperationException"/> at startup.
/// </remarks>
public sealed class PostgresAdvisoryLockMigrationRunner : IMigrationRunner
{
    // pg_try_advisory_lock key — deterministic, well-known, application-scoped.
    // Derived from: BitConverter.ToInt32(System.Text.Encoding.ASCII.GetBytes("saas"), 0)
    private const long AdvisoryLockKey = 1937010035L;

    private readonly IEnumerable<IDbContextMigrator> _migrators;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PostgresAdvisoryLockMigrationRunner> _logger;

    /// <summary>
    /// Initializes the runner.
    /// </summary>
    /// <param name="migrators">All registered <see cref="IDbContextMigrator"/> implementations.</param>
    /// <param name="configuration">Application configuration (for the advisory-lock connection string).</param>
    /// <param name="logger">Logger.</param>
    public PostgresAdvisoryLockMigrationRunner(
        IEnumerable<IDbContextMigrator> migrators,
        IConfiguration configuration,
        ILogger<PostgresAdvisoryLockMigrationRunner> logger)
    {
        _migrators = migrators ?? throw new ArgumentNullException(nameof(migrators));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task RunPendingAsync(CancellationToken ct = default)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required for the migration runner.");
        }

        await using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        bool lockAcquired = await TryAcquireAdvisoryLockAsync(conn, ct).ConfigureAwait(false);

        if (!lockAcquired)
        {
            _logger.LogInformation(
                "Migration advisory lock not acquired — another instance is running migrations. Skipping.");
            return;
        }

        _logger.LogInformation("Migration advisory lock acquired (key={Key}). Starting migrations.", AdvisoryLockKey);

        try
        {
            IReadOnlyList<IDbContextMigrator> ordered = TopologicalSort(_migrators.ToList());
            foreach (IDbContextMigrator migrator in ordered)
            {
                _logger.LogInformation("Running migrations for {MigratorType}...", migrator.GetType().Name);
                await migrator.MigrateAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Migrations complete for {MigratorType}.", migrator.GetType().Name);
            }
        }
        finally
        {
            // Lock is released automatically when the connection is closed.
            // Explicit release here for clarity.
            await ReleaseAdvisoryLockAsync(conn, ct).ConfigureAwait(false);
            _logger.LogInformation("Migration advisory lock released.");
        }
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(
        NpgsqlConnection conn,
        CancellationToken ct)
    {
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT pg_try_advisory_lock({AdvisoryLockKey})";
        object? result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is true;
    }

    private static async Task ReleaseAdvisoryLockAsync(
        NpgsqlConnection conn,
        CancellationToken ct)
    {
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT pg_advisory_unlock({AdvisoryLockKey})";
        await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns migrators in topological order respecting <see cref="IDbContextMigrator.DependsOn"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected.</exception>
    private static IReadOnlyList<IDbContextMigrator> TopologicalSort(
        IReadOnlyList<IDbContextMigrator> migrators)
    {
        // Map concrete type → migrator instance.
        Dictionary<Type, IDbContextMigrator> byType = migrators
            .ToDictionary(m => m.GetType());

        var sorted = new List<IDbContextMigrator>(migrators.Count);
        var visited = new HashSet<Type>();
        var inProgress = new HashSet<Type>();

        foreach (IDbContextMigrator migrator in migrators)
        {
            Visit(migrator, byType, sorted, visited, inProgress);
        }

        return sorted;
    }

    private static void Visit(
        IDbContextMigrator migrator,
        Dictionary<Type, IDbContextMigrator> byType,
        List<IDbContextMigrator> sorted,
        HashSet<Type> visited,
        HashSet<Type> inProgress)
    {
        Type migratorType = migrator.GetType();

        if (visited.Contains(migratorType))
        {
            return;
        }

        if (!inProgress.Add(migratorType))
        {
            throw new InvalidOperationException(
                $"Circular dependency detected in migration graph involving '{migratorType.FullName}'.");
        }

        foreach (Type dependency in migrator.DependsOn)
        {
            if (byType.TryGetValue(dependency, out IDbContextMigrator? dependencyMigrator))
            {
                Visit(dependencyMigrator, byType, sorted, visited, inProgress);
            }
        }

        inProgress.Remove(migratorType);
        visited.Add(migratorType);
        sorted.Add(migrator);
    }
}
