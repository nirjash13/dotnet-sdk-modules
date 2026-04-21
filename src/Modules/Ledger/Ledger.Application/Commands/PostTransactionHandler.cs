using System;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using MassTransit;

namespace Ledger.Application.Commands;

/// <summary>
/// Thin MassTransit consumer wrapper around <see cref="IPostTransactionService"/>.
/// Kept so saga orchestration and bus-mode callers can still reach the handler over
/// RabbitMQ. The synchronous HTTP write endpoint does NOT use this path — it injects
/// <see cref="IPostTransactionService"/> directly to avoid a broker round-trip.
/// </summary>
public sealed class PostTransactionHandler : IConsumer<PostTransactionCommand>
{
    private readonly IPostTransactionService _service;

    /// <summary>Initializes the consumer with the in-process execution service.</summary>
    public PostTransactionHandler(IPostTransactionService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PostTransactionCommand> context)
    {
        Result<Guid> result = await _service
            .ExecuteAsync(context.Message, context.CancellationToken)
            .ConfigureAwait(false);

        await context.RespondAsync(result).ConfigureAwait(false);
    }
}
