using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Ledger.Contracts;

namespace Ledger.Application.Queries;

/// <summary>
/// In-process execution surface for <see cref="GetAccountBalanceQuery"/>. The HTTP
/// endpoint uses this directly to avoid a RabbitMQ request/response round-trip on the
/// read path.
/// </summary>
public interface IGetAccountBalanceService
{
    /// <summary>Executes the query and returns the balance DTO or a not-found failure.</summary>
    Task<Result<AccountBalanceDto>> ExecuteAsync(GetAccountBalanceQuery query, CancellationToken ct = default);
}
