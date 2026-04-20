using System.Diagnostics.Metrics;

namespace Chassis.Host.Observability;

/// <summary>
/// Central registry of System.Diagnostics.Metrics instruments for the chassis host.
/// All instruments are created once at startup and referenced by the pipeline and middleware.
/// </summary>
/// <remarks>
/// Phase 7 will add outbox-lag and saga meters. Helpers are noted below but not registered yet
/// to keep the Phase 1 build surface clean.
/// </remarks>
internal static class ChassisMeters
{
    /// <summary>The root <see cref="Meter"/> for all chassis host instruments.</summary>
    public static readonly Meter Meter = new("Chassis.Host", "0.1.0");

    /// <summary>
    /// Histogram tracking the wall-clock duration of module load operations.
    /// Tags: <c>module</c> (module name).
    /// Unit: seconds.
    /// </summary>
    public static readonly Histogram<double> ModuleLoadDuration =
        Meter.CreateHistogram<double>(
            "chassis.module.load.duration",
            unit: "s",
            description: "Time taken to discover and initialize a module at host startup.");

    /// <summary>
    /// Histogram tracking the duration of command/query dispatch through the mediator pipeline.
    /// Tags: <c>module</c>, <c>command</c>, <c>transport</c> (inproc|bus).
    /// Unit: seconds.
    /// </summary>
    public static readonly Histogram<double> CommandDuration =
        Meter.CreateHistogram<double>(
            "chassis.commands.duration",
            unit: "s",
            description: "Time taken to dispatch a command or query through the mediator pipeline.");

    /// <summary>
    /// Counter tracking Postgres RLS denial events (PostgresException SQLSTATE 42501).
    /// Tags: <c>module</c>, <c>table</c>.
    /// </summary>
    public static readonly Counter<long> RlsDenials =
        Meter.CreateCounter<long>(
            "chassis.rls.denials",
            description: "Number of Postgres RLS policy denials (SQLSTATE 42501).");

    // Phase 7 additions (reserved — not registered yet):
    // public static readonly ObservableGauge<long> OutboxDepth = ...
    // public static readonly Histogram<double> OutboxLag = ...
    // public static readonly ObservableGauge<long> SagaActive = ...
}
