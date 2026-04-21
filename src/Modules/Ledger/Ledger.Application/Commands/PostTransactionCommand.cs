using System;
using Chassis.SharedKernel.Abstractions;

namespace Ledger.Application.Commands;

/// <summary>
/// Command to post a monetary transaction to a ledger account.
/// Dispatched via MassTransit Mediator; handled by <see cref="PostTransactionHandler"/>.
/// </summary>
public sealed class PostTransactionCommand : ICommand<Result<Guid>>
{
    /// <summary>Initializes the command with required fields.</summary>
    public PostTransactionCommand(
        Guid accountId,
        decimal amount,
        string currency,
        string? memo,
        Guid? idempotencyKey)
    {
        AccountId = accountId;
        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Memo = memo;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the target account identifier.</summary>
    public Guid AccountId { get; }

    /// <summary>Gets the monetary amount to post. May be negative (credit/reversal).</summary>
    public decimal Amount { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the optional human-readable description.</summary>
    public string? Memo { get; }

    /// <summary>
    /// Gets the optional client-supplied idempotency key.
    /// When supplied, a duplicate request with the same key returns the existing posting's Id
    /// instead of creating a second posting.
    /// </summary>
    public Guid? IdempotencyKey { get; }
}
