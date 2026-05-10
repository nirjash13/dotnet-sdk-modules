using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Authorization;

/// <summary>
/// Value object representing a discrete permission defined as Resource × Action × Scope.
/// Example: Resource="billing", Action="invoice.read", Scope="own" → key "billing.invoice.read:own".
/// </summary>
public sealed class Permission : IEquatable<Permission>
{
    // Private constructor — use factory method.
    private Permission()
    {
    }

    /// <summary>Gets the permission's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the resource category (e.g., "billing", "organizations", "members").</summary>
    public string Resource { get; private set; } = string.Empty;

    /// <summary>Gets the action within the resource (e.g., "invoice.read", "member.invite").</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the scope modifier (e.g., "own", "any", "org").
    /// Defaults to "any" when no restriction applies.
    /// </summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>Gets the canonical permission key: "{Resource}.{Action}:{Scope}".</summary>
    public string Key => $"{Resource}.{Action}:{Scope}";

    /// <summary>Creates a new <see cref="Permission"/>.</summary>
    public static Permission Create(Guid id, string resource, string action, string scope = "any")
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Permission id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new IdentityDomainException("Permission resource must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new IdentityDomainException("Permission action must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new IdentityDomainException("Permission scope must not be empty.");
        }

        return new Permission
        {
            Id = id,
            Resource = resource.Trim().ToLowerInvariant(),
            Action = action.Trim().ToLowerInvariant(),
            Scope = scope.Trim().ToLowerInvariant(),
        };
    }

    /// <inheritdoc />
    public bool Equals(Permission? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Permission p && Equals(p);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Key;
}
