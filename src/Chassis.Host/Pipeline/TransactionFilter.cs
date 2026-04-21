// Transaction filter design choice for Phase 1
//
// Two viable approaches:
//
// Option A — System.Transactions.TransactionScope:
//   Wrap the handler invocation in a TransactionScope(TransactionScopeAsyncFlowOption.Enabled).
//   Pro: ambient transaction automatically enlists EF Core and any other ADO.NET operation.
//   Con: distributed transaction escalation risk when using Postgres + other enlisted resources
//       simultaneously — even with ImplicitDistributedTransactions=false, the DTC error surface
//       is confusing. TransactionScope also adds ~10µs overhead per command.
//
// Option B — OTel Activity span only:
//   Open a named Activity span "chassis.command.scope" around the handler execution.
//   Handlers manage their own DbContext transactions explicitly (unit-of-work at the handler level).
//   Pro: zero distributed-transaction risk; simpler to reason about; works cleanly with EF Core's
//       implicit transaction-per-SaveChanges pattern.
//   Con: no ambient cross-resource transaction; each handler must call SaveChangesAsync.
//
// Decision: Option B for Phase 1.
// Rationale: Phase 3 (Ledger) is when we first need cross-resource atomicity (EF Core + Marten).
//   At that point we will introduce an explicit DbContext-scoped transaction in the handler
//   and share it with Marten via IDocumentSession.OpenSession(transaction). The TransactionFilter
//   will be revisited in Phase 3 with the Ledger use-case as the concrete driver.
//   Encoding TransactionScope semantics now — without a real use-case to test against — risks
//   under-specified behavior (what if a handler does NOT use EF Core? what is the rollback
//   boundary?). An Activity span is always correct.
//
// This filter WILL be expanded in Phase 3. The comment above will be updated when that happens.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MassTransit;

namespace Chassis.Host.Pipeline;

/// <summary>
/// MassTransit consume filter that opens an OpenTelemetry Activity span for the command scope.
/// </summary>
/// <remarks>
/// Phase 1 behaviour: opens an OTel Activity named <c>chassis.command.scope</c> and propagates
/// the span across the handler invocation. Handlers are responsible for their own
/// EF Core transactions (<c>SaveChangesAsync</c> wraps each command in an implicit transaction).
/// <para>
/// Phase 3 (Ledger module) will evolve this filter to support an explicit shared DbTransaction
/// when a handler requires cross-store atomicity (EF Core + Marten event store).
/// </para>
/// </remarks>
internal sealed class TransactionFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private static readonly ActivitySource _activitySource =
        new ActivitySource("Chassis.Host.Command", "0.1.0");

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("transaction");
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        using Activity? activity = _activitySource.StartActivity(
            "chassis.command.scope",
            ActivityKind.Internal);

        activity?.SetTag("command.type", typeof(T).Name);

        try
        {
            await next.Send(context).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
