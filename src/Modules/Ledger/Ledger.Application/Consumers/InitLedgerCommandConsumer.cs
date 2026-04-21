using System;
using System.Threading.Tasks;
using Ledger.Application.Abstractions;
using Ledger.Domain.Entities;
using MassTransit;
using Registration.Contracts;

namespace Ledger.Application.Consumers;

/// <summary>
/// Handles the <see cref="InitLedger"/> command sent by the registration saga.
/// Creates a default ledger account for the tenant and responds with <see cref="LedgerInitialized"/>.
/// </summary>
/// <remarks>
/// Saga-level idempotency: the saga sends this command exactly once on the happy path.
/// Retry idempotency is provided by the MT retry policy and unique constraint on the saga
/// correlation — if the consumer fails transiently and retries, the same account id will
/// not be created twice because the saga only reaches this state once.
/// </remarks>
public sealed class InitLedgerCommandConsumer(
    IAccountRepository accountRepository,
    ILedgerUnitOfWork unitOfWork) : IConsumer<InitLedger>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<InitLedger> context)
    {
        InitLedger command = context.Message;

        Account account = Account.Create(
            command.TenantId,
            $"{command.TenantId:N}-default",
            command.Currency);

        accountRepository.Add(account);
        await unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);

        await context.Publish(new LedgerInitialized
        {
            CorrelationId = command.CorrelationId,
            AccountId = account.Id,
        }).ConfigureAwait(false);
    }
}
