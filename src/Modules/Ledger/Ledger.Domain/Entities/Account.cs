using System;
using System.Collections.Generic;
using Chassis.SharedKernel.Tenancy;
using Ledger.Domain.DomainEvents;
using Ledger.Domain.Exceptions;
using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Entities;

/// <summary>
/// Aggregate root representing a ledger account within a tenant.
/// Accounts hold one or more <see cref="Posting"/> entries.
/// </summary>
/// <remarks>
/// All state mutations go through factory/mutator methods to enforce invariants.
/// EF Core uses a private parameterless constructor for materialisation.
/// </remarks>
public sealed class Account : ITenantScoped
{
    private readonly List<Posting> _postings = new List<Posting>();
    private readonly List<object> _domainEvents = new List<object>();

    // Private constructor — use Account.Create() factory.
    private Account()
    {
    }

    /// <summary>Gets the unique identifier of this account.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the tenant this account belongs to (RLS column).</summary>
    public Guid TenantId { get; private init; }

    /// <summary>Gets the display name of the account.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the 3-character ISO 4217 currency code for this account.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the postings on this account (read-only projection).</summary>
    public IReadOnlyList<Posting> Postings => _postings;

    /// <summary>Gets all pending domain events raised by this aggregate.</summary>
    public IReadOnlyList<object> DomainEvents => _domainEvents;

    /// <summary>
    /// Creates a new <see cref="Account"/> for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant this account belongs to. Must not be empty.</param>
    /// <param name="name">The display name. Must not be null or whitespace.</param>
    /// <param name="currency">The 3-character ISO 4217 currency code. Must be exactly 3 chars.</param>
    /// <returns>A new <see cref="Account"/> with no postings.</returns>
    public static Account Create(Guid tenantId, string name, string currency)
    {
        if (tenantId == Guid.Empty)
        {
            throw new LedgerDomainException("TenantId must not be empty when creating an account.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new LedgerDomainException("Account name must not be empty.");
        }

        // Validate currency via Money to reuse the 3-char validation.
        Money.From(0m, currency); // Throws LedgerDomainException if invalid.

        return new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Currency = currency.ToUpperInvariant(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Posts a monetary transaction to this account, creating and returning the posting.
    /// Raises a <see cref="LedgerTransactionPostedDomainEvent"/>.
    /// </summary>
    /// <param name="amount">The monetary amount to post.</param>
    /// <param name="memo">Optional description.</param>
    /// <param name="idempotencyKey">Optional client-supplied dedup key.</param>
    /// <returns>The created <see cref="Posting"/>.</returns>
    /// <exception cref="LedgerDomainException">Thrown when posting currency does not match account currency.</exception>
    public Posting Post(Money amount, string? memo = null, Guid? idempotencyKey = null)
    {
        if (amount is null)
        {
            throw new LedgerDomainException("Amount must not be null.");
        }

        if (!string.Equals(amount.Currency, Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new LedgerDomainException(
                $"Posting currency '{amount.Currency}' does not match account currency '{Currency}'.");
        }

        Posting posting = Posting.Create(
            tenantId: TenantId,
            accountId: Id,
            amount: amount,
            memo: memo,
            idempotencyKey: idempotencyKey);

        _postings.Add(posting);

        _domainEvents.Add(new LedgerTransactionPostedDomainEvent(
            tenantId: TenantId,
            accountId: Id,
            postingId: posting.Id,
            amount: posting.Amount.Amount,
            currency: posting.Amount.Currency,
            memo: posting.Memo,
            occurredAt: posting.OccurredAt));

        return posting;
    }

    /// <summary>Clears all pending domain events (called after events are dispatched).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
