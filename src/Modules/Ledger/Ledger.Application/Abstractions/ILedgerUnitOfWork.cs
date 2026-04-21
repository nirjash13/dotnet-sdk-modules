using System.Threading;
using System.Threading.Tasks;

namespace Ledger.Application.Abstractions;

/// <summary>
/// Unit of work abstraction for the Ledger bounded context.
/// Coordinates committing EF Core changes and Marten audit events in a single
/// logical transaction. See <c>LedgerUnitOfWork.CommitAsync</c> for the
/// implementation strategy (TransactionScope).
/// </summary>
public interface ILedgerUnitOfWork
{
    /// <summary>
    /// Persists all pending changes from the EF Core DbContext and flushes any
    /// pending Marten audit events within the same database transaction.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task CommitAsync(CancellationToken ct = default);
}
