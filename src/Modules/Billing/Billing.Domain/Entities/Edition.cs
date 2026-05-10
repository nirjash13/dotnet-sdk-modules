using System;
using System.Collections.Generic;
using Billing.Domain.Exceptions;

namespace Billing.Domain.Entities;

/// <summary>
/// Named bundle of entitlements that represents a tier in the billing catalog (e.g., Free, Pro, Enterprise).
/// Maps to one or more <see cref="Price"/> objects via a <see cref="Plan"/>.
/// </summary>
public sealed class Edition
{
    private readonly List<EntitlementGrant> _entitlements = new List<EntitlementGrant>();

    private Edition()
    {
    }

    /// <summary>Gets the internal identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the machine-readable key (e.g., "free", "pro", "enterprise").</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the human-readable name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the ordered list of entitlement grants included in this edition.</summary>
    public IReadOnlyList<EntitlementGrant> Entitlements => _entitlements;

    /// <summary>Gets the UTC timestamp when this edition was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Creates a new edition.</summary>
    public static Edition Create(string key, string name)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BillingDomainException("Edition key must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BillingDomainException("Edition name must not be empty.");
        }

        return new Edition
        {
            Id = Guid.NewGuid(),
            Key = key.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Adds an entitlement grant to this edition.</summary>
    public void AddEntitlement(EntitlementGrant grant)
    {
        if (grant is null)
        {
            throw new BillingDomainException("EntitlementGrant must not be null.");
        }

        _entitlements.Add(grant);
    }
}
