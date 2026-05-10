namespace Audit.Infrastructure.Options;

/// <summary>Configuration options for the Audit module read from <c>Audit</c> config section.</summary>
public sealed class AuditOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Audit";

    /// <summary>
    /// Gets or sets a value indicating whether the EF Core interceptor that auto-instruments
    /// <see cref="Audit.Application.Interceptors.AuditableAttribute"/>-decorated entities is active.
    /// Default is <see langword="false"/> — opt-in.
    /// </summary>
    public bool EnableEfCoreInterceptor { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the hash-chain decorator is enabled.
    /// When <see langword="true"/>, each entry's SHA-256 hash is chained to the previous entry.
    /// TODO(Phase 5.4): formal Merkle-style storage for Phase 8 SOC 2.
    /// </summary>
    public bool EnableHashChain { get; set; } = false;
}
