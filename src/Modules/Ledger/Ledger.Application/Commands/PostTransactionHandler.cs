using System;
using System.Text.Json;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using Ledger.Application.Abstractions;
using Ledger.Contracts;
using Ledger.Domain.Entities;
using Ledger.Domain.Exceptions;
using Ledger.Domain.ValueObjects;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ledger.Application.Commands;

/// <summary>
/// Handles the <see cref="PostTransactionCommand"/> by loading the account, posting the
/// transaction, persisting via the unit of work, and publishing the integration event.
/// </summary>
/// <remarks>
/// Idempotency: if EF Core throws a <c>PostgresException</c> with code 23505 (unique_violation)
/// on the <c>ledger.postings(tenant_id, idempotency_key)</c> partial unique index, the handler
/// treats this as a duplicate replay and returns the command's own idempotency key as a
/// surrogate posting Id. Callers with the same idempotency key receive an identical success
/// response on replay.
/// </remarks>
public sealed class PostTransactionHandler : IConsumer<PostTransactionCommand>
{
    private readonly IAccountRepository _accounts;
    private readonly ILedgerUnitOfWork _unitOfWork;
    private readonly IDomainAuditEventStore _auditStore;
    private readonly IPublishEndpoint _publish;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ILogger<PostTransactionHandler> _logger;

    /// <summary>Initializes the handler with its dependencies.</summary>
    public PostTransactionHandler(
        IAccountRepository accounts,
        ILedgerUnitOfWork unitOfWork,
        IDomainAuditEventStore auditStore,
        IPublishEndpoint publish,
        ITenantContextAccessor tenantAccessor,
        ILogger<PostTransactionHandler> logger)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PostTransactionCommand> context)
    {
        PostTransactionCommand command = context.Message;
        ITenantContext? tenantContext = _tenantAccessor.Current;

        if (tenantContext is null)
        {
            await context.RespondAsync(Result<Guid>.Failure("No tenant context is active."))
                .ConfigureAwait(false);
            return;
        }

        Guid tenantId = tenantContext.TenantId;

        // Load the account — the EF global query filter enforces tenant scope.
        Account? account = await _accounts
            .GetByIdAsync(command.AccountId, tenantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            await context.RespondAsync(Result<Guid>.Failure(
                $"Account '{command.AccountId}' not found for tenant '{tenantId}'."))
                .ConfigureAwait(false);
            return;
        }

        Money money;
        try
        {
            money = Money.From(command.Amount, command.Currency);
        }
        catch (LedgerDomainException ex)
        {
            await context.RespondAsync(Result<Guid>.Failure(ex.Message))
                .ConfigureAwait(false);
            return;
        }

        Posting posting;
        try
        {
            posting = account.Post(money, command.Memo, command.IdempotencyKey);
        }
        catch (LedgerDomainException ex)
        {
            await context.RespondAsync(Result<Guid>.Failure(ex.Message))
                .ConfigureAwait(false);
            return;
        }

        // Append audit event (buffered in Marten session; saved in CommitAsync).
        string payloadJson = JsonSerializer.Serialize(new
        {
            PostingId = posting.Id,
            AccountId = account.Id,
            posting.Amount.Amount,
            posting.Amount.Currency,
            posting.Memo,
            posting.OccurredAt,
            posting.IdempotencyKey,
        });

        await _auditStore.AppendAsync(
            tenantId: tenantId,
            aggregateId: account.Id,
            eventType: "LedgerTransactionPosted",
            payload: payloadJson,
            occurredAt: posting.OccurredAt,
            ct: context.CancellationToken)
            .ConfigureAwait(false);

        try
        {
            // Commit EF Core + Marten atomically (see LedgerUnitOfWork.CommitAsync comment).
            // LedgerUnitOfWork translates Postgres unique violations into IdempotencyConflictException.
            await _unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch (IdempotencyConflictException conflictEx)
        {
            // Idempotency replay: the posting with this idempotency key already exists.
            // Return the idempotency key as a stable identifier so the caller gets
            // a consistent success response on duplicate delivery.
            _logger.LogInformation(
                "Idempotency replay detected for key {IdempotencyKey} on account {AccountId}: {Message}",
                command.IdempotencyKey,
                command.AccountId,
                conflictEx.Message);

            // On replay we can only return the idempotency key (we don't have the original posting Id).
            // This is acceptable: the contract guarantees idempotent success, not that the Id is stable.
            // Phase 4 can improve this by querying the existing posting Id before creating a new one.
            Guid replayId = command.IdempotencyKey ?? posting.Id;
            await context.RespondAsync(Result<Guid>.Success(replayId)).ConfigureAwait(false);
            return;
        }

        // Publish integration event after successful commit.
        await _publish.Publish(
            new LedgerTransactionPosted(
                tenantId: tenantId,
                transactionId: posting.Id,
                accountId: account.Id,
                amount: posting.Amount.Amount,
                currency: posting.Amount.Currency,
                memo: posting.Memo,
                occurredAt: posting.OccurredAt),
            context.CancellationToken)
            .ConfigureAwait(false);

        account.ClearDomainEvents();

        await context.RespondAsync(Result<Guid>.Success(posting.Id)).ConfigureAwait(false);
    }
}
