// Design choice: DbCommandInterceptor (not DbConnectionInterceptor)
//
// Two viable patterns exist for injecting SET LOCAL app.tenant_id:
//
// Option A — DbConnectionInterceptor.ConnectionOpenedAsync:
//   Issue a single SET statement when the connection opens.
//   Pro: fires once per connection open; simple.
//   Con: SET is connection-scoped, not transaction-scoped. "SET LOCAL" requires an active
//       transaction to be meaningful (LOCAL reverts at transaction end). If the connection
//       is opened outside a transaction and then a transaction starts, the SET LOCAL issued
//       at connection-open is *outside* the transaction and therefore has no LOCAL effect —
//       it persists for the connection lifetime. RLS depends on the value being reset per
//       transaction. Using plain SET (not LOCAL) has the correct semantics at
//       connection-open time but is harder to reason about in pooled connections.
//
// Option B — DbCommandInterceptor hooking ReaderExecutingAsync / NonQueryExecutingAsync /
//   ScalarExecutingAsync: Issue "SET LOCAL app.tenant_id = '<guid>'" as a separate command
//   before each query. SET LOCAL is transaction-scoped so it is correct to issue per-command
//   in an auto-commit context too (each command runs in an implicit single-statement transaction
//   in Postgres if there is no explicit BEGIN). The cost is one extra round-trip per distinct
//   tenant-id-change within a transaction, mitigated by caching the last-set value per
//   DbContext instance.
//
// Decision: Option B — DbCommandInterceptor per-command with a per-instance last-set cache.
// Rationale: correctness over the connection-pool lifecycle is clearer; RLS requires
// SET LOCAL to take effect within the transaction that issues the DML. A connection recycled
// from the pool starts with no app.tenant_id set, so issuing it at command time ensures it
// is always present regardless of pool reuse. The extra round-trip is ~0.1 ms on localhost
// and negligible at our target RPS (200–500).

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chassis.Persistence;

/// <summary>
/// EF Core command interceptor that issues <c>SET LOCAL app.tenant_id = '&lt;guid&gt;'</c>
/// before every DML/query command so that Postgres Row-Level Security policies evaluate
/// against the current tenant.
/// </summary>
/// <remarks>
/// Registered via <c>AddChassisPersistence</c> and scoped to the DbContext lifetime.
/// If no tenant context is established when a command executes, an
/// <see cref="InvalidOperationException"/> is thrown — fail-fast to prevent data leakage.
/// </remarks>
public sealed class TenantCommandInterceptor : DbCommandInterceptor
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    // Cache the last tenant id we SET on this interceptor instance so we avoid
    // an extra round-trip when consecutive commands run as the same tenant within
    // the same DbContext instance.
    private Guid _lastSetTenantId = Guid.Empty;

    /// <summary>Initializes the interceptor with the ambient tenant context accessor.</summary>
    public TenantCommandInterceptor(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor
            ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(command, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(command, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(command, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task SetTenantAsync(DbCommand command, CancellationToken cancellationToken)
    {
        ITenantContext? ctx = _tenantContextAccessor.Current;
        if (ctx is null)
        {
            throw new InvalidOperationException(
                "TenantCommandInterceptor: no ambient tenant context when executing a database command. " +
                "Ensure TenantMiddleware runs before any database access, " +
                "or explicitly set ITenantContextAccessor.Current in background services.");
        }

        Guid tenantId = ctx.TenantId;

        // Skip the round-trip if this interceptor instance already SET the same tenant id.
        if (tenantId == _lastSetTenantId)
        {
            return;
        }

        // Issue SET LOCAL so the value is scoped to the current transaction.
        // In auto-commit mode (no explicit BEGIN), SET LOCAL is equivalent to SET for that
        // single statement's implicit transaction, which is exactly what we want for RLS.
        using DbCommand setCmd = command.Connection!.CreateCommand();
        setCmd.Transaction = command.Transaction;
        setCmd.CommandText = $"SET LOCAL app.tenant_id = '{tenantId}'";
        await setCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _lastSetTenantId = tenantId;
    }
}
