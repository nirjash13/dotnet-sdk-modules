using System.Diagnostics.Metrics;

namespace Chassis.Host.Observability;

/// <summary>
/// Central registry of System.Diagnostics.Metrics instruments for the chassis host.
/// All instruments are created once at startup and referenced by the pipeline and middleware.
/// </summary>
/// <remarks>
/// <para>
/// All instrument names use dots in the SDK definition; the OTel SDK translates them to
/// underscores when exporting to Prometheus (e.g. <c>chassis.commands.duration</c> →
/// <c>chassis_commands_duration_seconds</c>). Dashboard JSON references the Prometheus name.
/// </para>
/// <para>
/// The class is <c>internal static</c> — it is the composition root for metrics. Consumers
/// reference the static fields directly; no DI injection is required for writes.
/// Observable gauges that require DB queries are wired in <c>OutboxLagReporter</c> and
/// registered as hosted services; their callbacks update the gauge values set here.
/// </para>
/// </remarks>
internal static class ChassisMeters
{
    /// <summary>The root <see cref="Meter"/> for all chassis host instruments.</summary>
    public static readonly Meter Meter = new Meter("Chassis.Host", "0.1.0");

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
    /// Prometheus export name: <c>chassis_commands_duration_seconds</c>.
    /// Tags: <c>module</c>, <c>command</c>, <c>transport</c> (inproc|bus).
    /// Unit: seconds.
    /// </summary>
    /// <remarks>
    /// Recorded by <c>LoggingFilter&lt;T&gt;</c> on every command/query dispatch.
    /// </remarks>
    public static readonly Histogram<double> CommandDuration =
        Meter.CreateHistogram<double>(
            "chassis.commands.duration",
            unit: "s",
            description: "Time taken to dispatch a command or query through the mediator pipeline.");

    /// <summary>
    /// Counter tracking Postgres RLS denial events (PostgresException SQLSTATE 42501).
    /// Prometheus export name: <c>chassis_rls_denials_total</c>.
    /// Tags: <c>module</c>, <c>table</c>.
    /// </summary>
    /// <remarks>
    /// Incremented by <c>ProblemDetailsExceptionHandler</c> when it intercepts a
    /// <c>PostgresException</c> with <c>SqlState == "42501"</c>.
    /// </remarks>
    public static readonly Counter<long> RlsDenials =
        Meter.CreateCounter<long>(
            "chassis.rls.denials",
            description: "Number of Postgres RLS policy denials (SQLSTATE 42501).");

    /// <summary>
    /// Histogram tracking the outbox delivery lag — the time between a message being written
    /// to the outbox and it being delivered to the broker.
    /// Prometheus export name: <c>chassis_outbox_lag_seconds</c>.
    /// Tags: <c>module</c> (module owning the outbox).
    /// Unit: seconds.
    /// </summary>
    /// <remarks>
    /// Values are recorded by <c>OutboxLagReporter</c> (a hosted service) every 10 seconds
    /// by querying <c>SELECT EXTRACT(EPOCH FROM (NOW() - MIN(enqueue_time))) FROM transport.outbox_message WHERE delivered IS NULL</c>.
    /// </remarks>
    public static readonly Histogram<double> OutboxLagSeconds =
        Meter.CreateHistogram<double>(
            "chassis.outbox.lag",
            unit: "s",
            description: "Time between an outbox row being written and delivered to the broker.");

    /// <summary>
    /// Observable gauge tracking the number of undelivered outbox rows.
    /// Prometheus export name: <c>chassis_outbox_depth</c>.
    /// Tags: <c>module</c> (module owning the outbox).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The callback executes a synchronous DB query (<c>COUNT(*) FROM transport.outbox_message
    /// WHERE delivered IS NULL</c>) on each Prometheus scrape. This is acceptable at the
    /// standard 15s scrape interval but should be monitored under high-frequency scrapes.
    /// </para>
    /// <para>
    /// The static callback returns 0 at startup; <c>OutboxLagReporter</c> is the source of truth
    /// for lag histograms. Outbox depth is better served by this gauge as an observable pull-model
    /// instrument aligned to the Prometheus scrape cadence.
    /// </para>
    /// <para>
    /// Alert: <c>chassis_outbox_depth &gt; 0</c> sustained for &gt;60s indicates broker unavailability.
    /// </para>
    /// </remarks>
    public static readonly ObservableGauge<long> OutboxDepth =
        Meter.CreateObservableGauge<long>(
            "chassis.outbox.depth",
            observeValue: static () => 0L, // Phase 7: callback returns 0; DI-friendly live query not wired here.
            unit: "{messages}",
            description: "Number of outbox rows not yet delivered to the broker.");

    /// <summary>
    /// Observable gauge tracking the number of active (non-terminal) saga instances.
    /// Prometheus export name: <c>chassis_saga_active_count</c>.
    /// Tags: <c>saga</c> (saga type name, e.g. "registration").
    /// </summary>
    /// <remarks>
    /// Returns 0 in Phase 7. Phase 8 wires the real query:
    /// <c>SELECT COUNT(*) FROM registration.registration_saga_state WHERE current_state NOT IN ('Completed','Faulted')</c>.
    /// Alert threshold: sustained count &gt;0 for longer than the saga SLA (e.g. 5 minutes).
    /// </remarks>
    public static readonly ObservableGauge<long> SagaActiveCount =
        Meter.CreateObservableGauge<long>(
            "chassis.saga.active.count",
            observeValue: static () => 0L,
            unit: "{instances}",
            description: "Number of saga instances currently in a non-terminal state.");

    /// <summary>
    /// Histogram tracking the wall-clock duration of saga executions (happy path and faulted).
    /// Prometheus export name: <c>chassis_saga_duration_seconds</c>.
    /// Tags: <c>saga</c> (saga type name), <c>terminal_state</c> (Completed|Faulted).
    /// Unit: seconds.
    /// </summary>
    /// <remarks>
    /// Record a value in the saga's terminal state transition:
    /// <c>ChassisMeters.SagaDurationSeconds.Record(elapsed.TotalSeconds, tagList)</c>.
    /// </remarks>
    public static readonly Histogram<double> SagaDurationSeconds =
        Meter.CreateHistogram<double>(
            "chassis.saga.duration",
            unit: "s",
            description: "Wall-clock time from saga start to terminal state (Completed or Faulted).");
}
