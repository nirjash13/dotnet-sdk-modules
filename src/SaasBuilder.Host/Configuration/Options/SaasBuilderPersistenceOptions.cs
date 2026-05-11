namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling EF Core persistence behavior in the SaasBuilder host.
/// </summary>
public sealed class SaasBuilderPersistenceOptions
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Migrations.IMigrationRunner.RunPendingAsync"/>
    /// is called automatically during application startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, the migration runner runs on <see cref="Microsoft.Extensions.Hosting.IHostedLifecycle.StartedAsync"/>
    /// before the first request is processed. The runner acquires a Postgres advisory lock
    /// so only one instance runs migrations in a rolling deployment.
    /// </para>
    /// <para>
    /// Defaults to <see langword="false"/> to preserve backward compatibility.
    /// Set to <see langword="true"/> in production to eliminate the need to run migrations
    /// out-of-band before deployment.
    /// </para>
    /// </remarks>
    public bool MigrateOnStartup { get; set; }
}
