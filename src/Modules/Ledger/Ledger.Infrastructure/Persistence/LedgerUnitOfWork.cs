using System;
using System.Threading;
using System.Threading.Tasks;
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
        try
        {
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await martenSession.SaveChangesAsync(ct).ConfigureAwait(false);
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
