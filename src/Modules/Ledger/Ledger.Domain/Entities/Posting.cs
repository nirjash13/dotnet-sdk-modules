using System;
using Chassis.SharedKernel.Tenancy;
using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Entities;

/// <summary>
/// Represents a single monetary entry on a ledger account.
/// Postings are immutable after creation; reversals are achieved via counter-postings.
/// </summary>
public sealed class Posting : ITenantScoped
{
    // Backing field for Money owned type — required because Money is a value object
    // with private constructor; EF Core's owned-type mapping populates this field
    // via property access. The field is placed before the constructor (SA1201 compliant).
    // IDE0032 suppressed: auto-property cannot be used here because the private setter
    // must accept the owned type reconstructed by EF Core's materialiser.
#pragma warning disable IDE0032 // Use auto property — inapplicable for EF Core owned-type backing field
    private Money _amount = Money.From(0m, "USD");
#pragma warning restore IDE0032

    // Private constructor — use Account.Post() to create postings.
    private Posting()
    {
        // EF Core parameterless constructor. _amount is initialised above with a sentinel value;
        // EF Core's owned-type materialiser replaces it with the persisted value on load.
    }

    /// <summary>Gets the unique identifier for this posting.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the tenant that owns this posting (RLS column).</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the account this posting belongs to.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Gets the monetary amount of the posting.</summary>
    public Money Amount
    {
        get => _amount;
        private set => _amount = value;
    }

    /// <summary>Gets the UTC timestamp when the posting was recorded.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Gets the optional human-readable description of the posting.</summary>
    public string? Memo { get; private set; }

    /// <summary>
    /// Gets the optional client-supplied idempotency key.
    /// A unique partial index on <c>(tenant_id, idempotency_key)</c> WHERE idempotency_key IS NOT NULL
    /// ensures that duplicate requests with the same key produce exactly one posting.
    /// </summary>
    public Guid? IdempotencyKey { get; private set; }

    /// <summary>
    /// Creates a new <see cref="Posting"/> with the supplied values.
    /// </summary>
    internal static Posting Create(
        Guid tenantId,
        Guid accountId,
        Money amount,
        string? memo,
        Guid? idempotencyKey)
    {
        if (tenantId == Guid.Empty)
        {
            throw new Exceptions.LedgerDomainException("TenantId must not be empty.");
        }

        if (accountId == Guid.Empty)
        {
            throw new Exceptions.LedgerDomainException("AccountId must not be empty.");
        }

        return new Posting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = accountId,
            _amount = amount,
            OccurredAt = DateTimeOffset.UtcNow,
            Memo = memo,
            IdempotencyKey = idempotencyKey,
        };
    }
}
