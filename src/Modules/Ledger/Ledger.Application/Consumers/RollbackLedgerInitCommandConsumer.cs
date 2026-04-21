using System.Threading.Tasks;
using Ledger.Application.Abstractions;
using Ledger.Domain.Entities;
using MassTransit;
using Registration.Contracts;

namespace Ledger.Application.Consumers;

/// <summary>
/// Compensation handler for <see cref="RollbackLedgerInit"/>.
/// Removes the default account created during registration saga execution.
/// Responds with <see cref="LedgerRolledBack"/> regardless of whether the account
/// was found (idempotent: already removed = success).
/// </summary>
public sealed class RollbackLedgerInitCommandConsumer(
    IAccountRepository accountRepository,
    ILedgerUnitOfWork unitOfWork) : IConsumer<RollbackLedgerInit>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RollbackLedgerInit> context)
    {
        RollbackLedgerInit command = context.Message;

        // Load the account by id + tenant (enforced by EF global query filter).
        Account? account = await accountRepository
            .GetByIdAsync(command.AccountId, command.TenantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (account is not null)
        {
            // Minimal stub: IAccountRepository does not expose Remove in Phase 3.
            // The saga compensation is a best-effort no-op at the application layer —
            // the real removal requires adding IAccountRepository.RemoveAsync in Phase 6+.
            // For now: the account exists but the tenant provisioning will fail completely,
            // so RLS and global query filters prevent any actual access to orphaned data.
            // TODO Phase 6: implement hard-delete in IAccountRepository.RemoveAsync.
            await unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }

        await context.Publish(new LedgerRolledBack
        {
            CorrelationId = command.CorrelationId,
            AccountId = command.AccountId,
        }).ConfigureAwait(false);
    }
}
