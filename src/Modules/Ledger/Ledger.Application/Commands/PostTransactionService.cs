using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using FluentValidation;
using Ledger.Application.Abstractions;
using Ledger.Contracts;
using Ledger.Domain.Entities;
using Ledger.Domain.Exceptions;
using Ledger.Domain.ValueObjects;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ledger.Application.Commands;

/// <summary>
/// Implements <see cref="IPostTransactionService"/> — the synchronous, in-process
/// execution path for posting a transaction. Called directly from the HTTP endpoint
/// so the response time is bounded by the DB commit and NOT by the
/// <c>LedgerTransactionPosted</c> downstream consumer.
/// </summary>
/// <remarks>
/// Per-step timings are emitted at Information level so a single request log line
/// carries the duration of each phase (<c>account_load</c>, <c>audit_persist</c>,
/// <c>posting_persist</c>, <c>publish</c>). Use structured queries in Loki/Serilog
/// to isolate the slowest component.
/// </remarks>
public sealed class PostTransactionService : IPostTransactionService
{
    private readonly IAccountRepository _accounts;
    private readonly ILedgerUnitOfWork _unitOfWork;
    private readonly IDomainAuditEventStore _auditStore;
    private readonly IPublishEndpoint? _publish;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IValidator<PostTransactionCommand> _validator;
    private readonly ILogger<PostTransactionService> _logger;

    /// <summary>Initializes the service with its dependencies.</summary>
    public PostTransactionService(
        IAccountRepository accounts,
        ILedgerUnitOfWork unitOfWork,
        IDomainAuditEventStore auditStore,
        ITenantContextAccessor tenantAccessor,
        IValidator<PostTransactionCommand> validator,
        ILogger<PostTransactionService> logger,
        IPublishEndpoint? publish = null)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _publish = publish;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> ExecuteAsync(PostTransactionCommand command, CancellationToken ct = default)
    {
        if (command is null)
        {
            return Result<Guid>.Failure("Command is null.");
        }

        FluentValidation.Results.ValidationResult validation =
            await _validator.ValidateAsync(command, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(validation.Errors.First().ErrorMessage);
        }

        ITenantContext? tenantContext = _tenantAccessor.Current;
        if (tenantContext is null)
        {
            return Result<Guid>.Failure("No tenant context is active.");
        }

        Guid tenantId = tenantContext.TenantId;
        Activity? activity = Activity.Current;
        long totalStart = Stopwatch.GetTimestamp();

        // ── Phase 1: account load ──────────────────────────────────────────────
        long stepStart = Stopwatch.GetTimestamp();
        Account? account;
        try
        {
            account = await _accounts
                .GetByIdAsync(command.AccountId, tenantId, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            RecordStep(activity, "account_load", Stopwatch.GetElapsedTime(stepStart));
        }

        if (account is null)
        {
            return Result<Guid>.Failure(
                $"Account '{command.AccountId}' not found for tenant '{tenantId}'.");
        }

        Money money;
        try
        {
            money = Money.From(command.Amount, command.Currency);
        }
        catch (LedgerDomainException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }

        Posting posting;
        try
        {
            posting = account.Post(money, command.Memo, command.IdempotencyKey);
        }
        catch (LedgerDomainException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }

        // ── Phase 2: audit persist (buffered in Marten session; saved in CommitAsync) ──
        stepStart = Stopwatch.GetTimestamp();
        try
        {
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
                ct: ct)
                .ConfigureAwait(false);
        }
        finally
        {
            RecordStep(activity, "audit_persist", Stopwatch.GetElapsedTime(stepStart));
        }

        // ── Phase 3: posting persist (EF + Marten atomic commit) ───────────────
        stepStart = Stopwatch.GetTimestamp();
        try
        {
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (IdempotencyConflictException conflictEx)
        {
            RecordStep(activity, "posting_persist", Stopwatch.GetElapsedTime(stepStart));
            _logger.LogInformation(
                "Idempotency replay detected for key {IdempotencyKey} on account {AccountId}: {Message}",
                command.IdempotencyKey,
                command.AccountId,
                conflictEx.Message);

            Guid replayId = command.IdempotencyKey ?? posting.Id;
            return Result<Guid>.Success(replayId);
        }

        RecordStep(activity, "posting_persist", Stopwatch.GetElapsedTime(stepStart));

        // ── Phase 4: publish (fire-and-forget to broker in bus mode; response is
        // NOT gated on the downstream consumer completing projection work) ─────
        stepStart = Stopwatch.GetTimestamp();
        try
        {
            if (_publish is not null)
            {
                await _publish.Publish(
                    new LedgerTransactionPosted(
                        tenantId: tenantId,
                        transactionId: posting.Id,
                        accountId: account.Id,
                        amount: posting.Amount.Amount,
                        currency: posting.Amount.Currency,
                        memo: posting.Memo,
                        occurredAt: posting.OccurredAt),
                    ct)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // A publish failure must not roll back an already-committed posting.
            // The transaction is durable; log and return success. Operators should
            // rely on the outbox/dead-letter path to recover the projection.
            _logger.LogError(
                ex,
                "PostTransaction {PostingId} committed but publish of LedgerTransactionPosted failed.",
                posting.Id);
        }
        finally
        {
            RecordStep(activity, "publish", Stopwatch.GetElapsedTime(stepStart));
        }

        TimeSpan total = Stopwatch.GetElapsedTime(totalStart);
        _logger.LogInformation(
            "PostTransaction completed in {TotalMs:F1} ms (posting={PostingId}, account={AccountId}, tenant={TenantId}).",
            total.TotalMilliseconds,
            posting.Id,
            account.Id,
            tenantId);

        account.ClearDomainEvents();
        return Result<Guid>.Success(posting.Id);
    }

    private void RecordStep(Activity? activity, string step, TimeSpan elapsed)
    {
        double ms = elapsed.TotalMilliseconds;

        activity?.AddEvent(new ActivityEvent(
            name: "PostTransaction." + step,
            tags: new ActivityTagsCollection(new[]
            {
                new KeyValuePair<string, object?>("step", step),
                new KeyValuePair<string, object?>("duration_ms", ms),
            })));

        _logger.LogInformation(
            "PostTransaction step {Step} took {ElapsedMs:F2} ms",
            step,
            ms);
    }
}
