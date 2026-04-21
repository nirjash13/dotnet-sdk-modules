using System;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Ledger.Contracts;
using MassTransit;

namespace Ledger.Application.Queries;

/// <summary>
/// Thin MassTransit consumer wrapper around <see cref="IGetAccountBalanceService"/>.
/// Kept for bus-mode callers; the HTTP read endpoint uses the service directly.
/// </summary>
public sealed class GetAccountBalanceHandler : IConsumer<GetAccountBalanceQuery>
{
    private readonly IGetAccountBalanceService _service;

    /// <summary>Initializes the consumer with the in-process execution service.</summary>
    public GetAccountBalanceHandler(IGetAccountBalanceService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<GetAccountBalanceQuery> context)
    {
        Result<AccountBalanceDto> result = await _service
            .ExecuteAsync(context.Message, context.CancellationToken)
            .ConfigureAwait(false);

        await context.RespondAsync(result).ConfigureAwait(false);
    }
}
