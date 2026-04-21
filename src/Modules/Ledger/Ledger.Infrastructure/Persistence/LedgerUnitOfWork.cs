using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Ledger.Application.Abstractions;
using Ledger.Domain.Exceptions;
using Marten;
using Npgsql;

namespace Ledger.Infrastructure.Persistence;

/// <summary>
/// Unit of work that commits EF Core changes and Marten audit events atomically.
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomic write strategy:</b> EF Core and Marten each manage their own Npgsql connection.
/// To achieve a single logical commit, we use <see cref="TransactionScope"/> with
/// <see cref="TransactionScopeAsyncFlowOption.Enabled"/> and
/// <see cref="TransactionScopeOption.Required"/>.
/// </para>
/// <para>
/// Both EF Core's <c>SaveChangesAsync</c> and Marten's <c>SaveChangesAsync</c> enlist
/// in the ambient <see cref="TransactionScope"/> automatically when their underlying
/// Npgsql connections are opened inside the scope. When <c>scope.Complete()</c> is called,
/// the .NET Distributed Transaction Coordinator (MSDTC) or Npgsql's single-phase
/// optimisation commits both operations together.
/// </para>
/// <para>
/// <b>Single-phase optimisation:</b> When both EF Core and Marten connect to the same
/// Postgres instance (same connection string), Npgsql uses a single-phase commit for the
/// second connection enlistment, avoiding the full 2PC overhead of MSDTC. This is the
/// common case in this chassis.
/// </para>
/// <para>
/// <b>Limitation:</b> If EF Core and Marten use different connection strings (different
/// Postgres servers), MSDTC is required. Ensure MSDTC is enabled in that scenario.
/// Phase 4 can replace this with a shared <c>DbTransaction</c> if both contexts share
/// the same Npgsql connection instance.
/// </para>
/// </remarks>
internal sealed class LedgerUnitOfWork(LedgerDbContext context, IDocumentSession martenSession)
    : ILedgerUnitOfWork
{
    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken ct = default)
    {
        // TransactionScope with async flow ensures the ambient transaction context
        // propagates across await boundaries (required for .NET async/await).
        //
        // TransactionScopeOption.Required: joins an existing ambient transaction if one
        // exists, or creates a new one. Since handler code runs in an HTTP request scope
        // with no pre-existing TransactionScope, this always creates a new scope here.
        try
        {
            using TransactionScope scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TimeSpan.FromSeconds(30),
                },
                TransactionScopeAsyncFlowOption.Enabled);

            // 1. EF Core SaveChanges — enlists in the ambient TransactionScope.
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // 2. Marten SaveChanges — enlists in the same ambient TransactionScope.
            //    Marten's Npgsql connection is opened inside the scope, so it auto-enlists.
            await martenSession.SaveChangesAsync(ct).ConfigureAwait(false);

            // Mark the scope as complete — both participants commit on Dispose().
            scope.Complete();
        }
        catch (Exception ex) when (IsUniqueViolation(ex, out Guid idempotencyKey))
        {
            // Translate Postgres unique violation on (tenant_id, idempotency_key) into a
            // domain exception so the Application layer doesn't need to reference Npgsql.
            throw new IdempotencyConflictException(idempotencyKey, ex);
        }
    }

    private static bool IsUniqueViolation(Exception ex, out Guid idempotencyKey)
    {
        idempotencyKey = Guid.Empty;
        Exception? current = ex;
        while (current is not null)
        {
            if (current is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
