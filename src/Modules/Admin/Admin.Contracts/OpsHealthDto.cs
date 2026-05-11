using System.Collections.Generic;

namespace Admin.Contracts;

/// <summary>
/// Aggregated operations health status returned by the ops health endpoint.
/// </summary>
public sealed class OpsHealthDto
{
    /// <summary>Gets or sets the database connectivity status.</summary>
    public ComponentStatus DbStatus { get; set; }

    /// <summary>Gets or sets the message queue connectivity status.</summary>
    public ComponentStatus QueueStatus { get; set; }

    /// <summary>Gets or sets the per-provider health checks.</summary>
    public IReadOnlyList<ProviderHealthDto> Providers { get; set; } =
        new List<ProviderHealthDto>();

    /// <summary>Gets or sets the overall SLO status.</summary>
    public ComponentStatus SloStatus { get; set; }

    /// <summary>Gets or sets the aggregate health status.</summary>
    public ComponentStatus Overall { get; set; }
}

/// <summary>
/// Health status of a single external provider.
/// </summary>
public sealed class ProviderHealthDto
{
    /// <summary>Gets or sets the provider name (e.g., "stripe", "smtp").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the connectivity status.</summary>
    public ComponentStatus Status { get; set; }

    /// <summary>Gets or sets the measured latency in milliseconds, if available.</summary>
    public double? LatencyMs { get; set; }
}

/// <summary>
/// Health status values for components and providers.
/// </summary>
public enum ComponentStatus
{
    /// <summary>Component is healthy.</summary>
    Healthy = 0,

    /// <summary>Component is degraded but partially functional.</summary>
    Degraded = 1,

    /// <summary>Component is unhealthy or unreachable.</summary>
    Unhealthy = 2,

    /// <summary>Component health is unknown or not checked.</summary>
    Unknown = 3,
}
