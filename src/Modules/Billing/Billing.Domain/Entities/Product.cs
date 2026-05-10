using System;
using Billing.Domain.Exceptions;

namespace Billing.Domain.Entities;

/// <summary>
/// Represents a product in the billing catalog, synced from the external billing provider.
/// </summary>
public sealed class Product
{
    // Private constructor — use Product.Create() factory.
    private Product()
    {
    }

    /// <summary>Gets the internal product identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the provider-side product identifier (e.g., Stripe prod_xxx).</summary>
    public string ProviderId { get; private set; } = string.Empty;

    /// <summary>Gets the human-readable product name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional product description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets a value indicating whether this product is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC timestamp when the product was created locally.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new <see cref="Product"/> with the given catalog data.
    /// </summary>
    public static Product Create(string providerId, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new BillingDomainException("Product providerId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BillingDomainException("Product name must not be empty.");
        }

        return new Product
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId.Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Deactivates this product.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Updates the product name and description from the provider sync.</summary>
    public void SyncFromProvider(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BillingDomainException("Product name must not be empty.");
        }

        Name = name.Trim();
        Description = description?.Trim();
    }
}
