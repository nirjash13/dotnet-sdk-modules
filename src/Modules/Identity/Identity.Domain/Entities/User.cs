using System;
using System.Collections.Generic;
using Identity.Domain.DomainEvents;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// Aggregate root representing an authenticated identity in the system.
/// </summary>
/// <remarks>
/// Phase 2 ships a minimal User with email and display name.
/// Password hashing is deferred — no end-user password flow in Phase 2 acceptance criteria.
/// TODO Phase 3: add IPasswordHasher wiring once password-based sign-in is implemented.
/// </remarks>
public sealed class User
{
    private readonly List<UserTenantMembership> _memberships = new List<UserTenantMembership>();
    private readonly List<object> _domainEvents = new List<object>();

    // Private constructor — use factory methods to enforce invariants.
    private User()
    {
    }

    /// <summary>Gets the user's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the user's email address (unique per installation).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Gets the user's display name.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the user was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp of the user's last modification, if any.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Gets the tenant memberships for this user (read-only projection).</summary>
    public IReadOnlyList<UserTenantMembership> Memberships => _memberships;

    /// <summary>Gets all pending domain events raised by this aggregate.</summary>
    public IReadOnlyList<object> DomainEvents => _domainEvents;

    /// <summary>
    /// Creates a new <see cref="User"/> with the supplied identity.
    /// </summary>
    /// <param name="id">The unique identifier. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="email">The user's email address. Must not be null or whitespace.</param>
    /// <param name="displayName">The user's display name. Must not be null or whitespace.</param>
    /// <returns>A new <see cref="User"/> instance with a <see cref="UserCreatedDomainEvent"/> queued.</returns>
    public static User Create(Guid id, string email, string displayName)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("User id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityDomainException("User email must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new IdentityDomainException("User display name must not be empty.");
        }

        var user = new User
        {
            Id = id,
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        user._domainEvents.Add(new UserCreatedDomainEvent(user.Id, user.Email));
        return user;
    }

    /// <summary>
    /// Adds the user to the specified tenant with the given roles.
    /// </summary>
    /// <param name="tenantId">The tenant to join.</param>
    /// <param name="roles">The roles assigned within this tenant.</param>
    /// <param name="isPrimary">Whether this is the user's primary tenant (used for token enrichment).</param>
    /// <returns>The created <see cref="UserTenantMembership"/>.</returns>
    public UserTenantMembership AddMembership(Guid tenantId, string[] roles, bool isPrimary)
    {
        if (tenantId == Guid.Empty)
        {
            throw new IdentityDomainException("TenantId must not be empty when adding membership.");
        }

        var membership = UserTenantMembership.Create(Guid.NewGuid(), Id, tenantId, roles, isPrimary);
        _memberships.Add(membership);
        UpdatedAt = DateTimeOffset.UtcNow;
        return membership;
    }

    /// <summary>Clears all pending domain events (called after events are dispatched).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
