using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;

namespace Ledger.Application.Commands;

/// <summary>
/// In-process execution surface for <see cref="PostTransactionCommand"/>. The HTTP
/// endpoint injects this directly to avoid a RabbitMQ request/response round-trip on
/// the synchronous write path; downstream projection (LedgerTransactionPosted) is
/// published from inside and is not part of the response path under bus mode.
/// </summary>
public interface IPostTransactionService
{
    /// <summary>Executes the command and returns the posting id or a failure.</summary>
    Task<Result<System.Guid>> ExecuteAsync(PostTransactionCommand command, CancellationToken ct = default);
}
