using System;
using System.Collections.Generic;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// Represents a user-scoped or organization-scoped API key.
/// The raw key is returned exactly once at creation (never stored).
/// The stored <see cref="KeyHash"/> is SHA-256 of the raw key prefixed with "sk_".
/// </summary>
public sealed class ApiKey
{
    private ApiKey()
    {
    }

    /// <summary>Gets the API key's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the user who owns this key (null for org-scoped keys).</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Gets the organization that owns this key (null for user-scoped keys).</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Gets a display name for this key (set by the user).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the first 8 characters of the raw key (for display / lookup — not a secret).</summary>
    public string KeyPrefix { get; private set; } = string.Empty;

    /// <summary>Gets the SHA-256 hash of the full raw key (stored for validation).</summary>
    public string KeyHash { get; private set; } = string.Empty;

    /// <summary>Gets the JSON-serialized array of scopes granted to this key.</summary>
    public string ScopesJson { get; private set; } = "[]";

    /// <summary>Gets the UTC time when this key was last used for authentication.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>Gets the UTC time at which this key was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC time at which this key was revoked, or <see langword="null"/> if active.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Gets a value indicating whether the key is active (not revoked).</summary>
    public bool IsActive => RevokedAt is null;

    /// <summary>Creates a new user-scoped API key.</summary>
    public static ApiKey CreateUserScoped(
        Guid id,
        Guid userId,
        string name,
        string keyPrefix,
        string keyHash,
        IReadOnlyList<string> scopes)
    {
        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId must not be empty for a user-scoped API key.");
        }

        return Create(id, userId, null, name, keyPrefix, keyHash, scopes);
    }

    /// <summary>Creates a new organization-scoped API key.</summary>
    public static ApiKey CreateOrgScoped(
        Guid id,
        Guid organizationId,
        string name,
        string keyPrefix,
        string keyHash,
        IReadOnlyList<string> scopes)
    {
        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("OrganizationId must not be empty for an org-scoped API key.");
        }

        return Create(id, null, organizationId, name, keyPrefix, keyHash, scopes);
    }

    /// <summary>Updates the last-used timestamp (called on every successful authentication).</summary>
    public void RecordUsage()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Revokes the key, preventing future use.</summary>
    public void Revoke()
    {
        if (RevokedAt is not null)
        {
            throw new IdentityDomainException("API key is already revoked.");
        }

        RevokedAt = DateTimeOffset.UtcNow;
    }

    private static ApiKey Create(
        Guid id,
        Guid? userId,
        Guid? organizationId,
        string name,
        string keyPrefix,
        string keyHash,
        IReadOnlyList<string> scopes)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("ApiKey id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException("API key name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(keyHash))
        {
            throw new IdentityDomainException("Key hash must not be empty.");
        }

        return new ApiKey
        {
            Id = id,
            UserId = userId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            ScopesJson = System.Text.Json.JsonSerializer.Serialize(scopes),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
