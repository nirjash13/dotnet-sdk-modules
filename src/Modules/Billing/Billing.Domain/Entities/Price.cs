using System;
using Billing.Domain.Exceptions;
using Billing.Domain.ValueObjects;

namespace Billing.Domain.Entities;

/// <summary>
/// Represents a price attached to a <see cref="Product"/>.
/// Maps directly to a Stripe/Paddle price object.
/// </summary>
public sealed class Price
{
    private Price()
    {
    }

    /// <summary>Gets the internal price identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the parent product identifier.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Gets the provider-side price identifier (e.g., Stripe price_xxx).</summary>
    public string ProviderId { get; private set; } = string.Empty;

    /// <summary>Gets the pricing model (one-time, recurring, tiered, etc.).</summary>
    public PriceModel Model { get; private set; }

    /// <summary>Gets the price amount in the smallest currency unit (cents/pence).</summary>
    public long UnitAmountCents { get; private set; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the recurring interval when <see cref="Model"/> is <see cref="PriceModel.Recurring"/>.
    /// Null for one-time prices.
    /// </summary>
    public BillingInterval? Interval { get; private set; }

    /// <summary>Gets a value indicating whether this price is currently active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC timestamp when the price was created locally.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Creates a new <see cref="Price"/>.</summary>
    public static Price Create(
        Guid productId,
        string providerId,
        PriceModel model,
        long unitAmountCents,
        string currency,
        BillingInterval? interval = null)
    {
        if (productId == Guid.Empty)
        {
            throw new BillingDomainException("Price productId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new BillingDomainException("Price providerId must not be empty.");
        }

        if (unitAmountCents < 0)
        {
            throw new BillingDomainException("Price unitAmountCents must not be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new BillingDomainException("Price currency must be a 3-character ISO 4217 code.");
        }

        if (model == PriceModel.Recurring && interval is null)
        {
            throw new BillingDomainException("Recurring prices must specify a billing interval.");
        }

        return new Price
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProviderId = providerId.Trim(),
            Model = model,
            UnitAmountCents = unitAmountCents,
            Currency = currency.ToUpperInvariant(),
            Interval = interval,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Deactivates this price.</summary>
    public void Deactivate() => IsActive = false;
}
