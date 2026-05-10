using System;
using System.Collections.Generic;
using Billing.Domain.Exceptions;

namespace Billing.Domain.Entities;

/// <summary>
/// What is sold to a tenant — a combination of a <see cref="Product"/> and one or more
/// <see cref="Price"/> objects, optionally mapped to an <see cref="Edition"/>.
/// </summary>
public sealed class Plan
{
    private readonly List<Guid> _priceIds = new List<Guid>();

    private Plan()
    {
    }

    /// <summary>Gets the internal plan identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the parent product identifier.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Gets the human-readable plan name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional edition identifier that defines the feature set for this plan.</summary>
    public Guid? EditionId { get; private set; }

    /// <summary>Gets the price identifiers included in this plan.</summary>
    public IReadOnlyList<Guid> PriceIds => _priceIds;

    /// <summary>Gets a value indicating whether this plan is available for new subscriptions.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC timestamp when this plan was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Creates a new plan.</summary>
    public static Plan Create(Guid productId, string name, Guid? editionId = null)
    {
        if (productId == Guid.Empty)
        {
            throw new BillingDomainException("Plan productId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BillingDomainException("Plan name must not be empty.");
        }

        return new Plan
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = name.Trim(),
            EditionId = editionId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Adds a price to this plan.</summary>
    public void AddPrice(Guid priceId)
    {
        if (priceId == Guid.Empty)
        {
            throw new BillingDomainException("PriceId must not be empty.");
        }

        if (!_priceIds.Contains(priceId))
        {
            _priceIds.Add(priceId);
        }
    }

    /// <summary>Deactivates this plan so it cannot be used for new subscriptions.</summary>
    public void Deactivate() => IsActive = false;
}
