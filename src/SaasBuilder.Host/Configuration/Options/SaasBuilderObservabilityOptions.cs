namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling OpenTelemetry observability (tracing, metrics, logging).
/// </summary>
public sealed class SaasBuilderObservabilityOptions
{
    /// <summary>Gets a value indicating whether observability is enabled. Defaults to <c>true</c>.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Enables OpenTelemetry tracing and metrics export (default).</summary>
    public SaasBuilderObservabilityOptions Enable()
    {
        IsEnabled = true;
        return this;
    }

    /// <summary>
    /// Disables OpenTelemetry wiring entirely. Use in test environments where
    /// OTLP export is not available and noise reduction is desired.
    /// </summary>
    public SaasBuilderObservabilityOptions Disable()
    {
        IsEnabled = false;
        return this;
    }
}
