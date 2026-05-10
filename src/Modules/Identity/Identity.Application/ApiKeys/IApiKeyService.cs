using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.ApiKeys;

/// <summary>
/// Abstraction for user and organization-scoped API key management.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): API key creation, rotation, and revocation.
/// Keys are hashed at rest (SHA-256 prefix stored for lookup; full hash for verification).
/// Rotation endpoint: generates new key and invalidates the old one.
/// </remarks>
public interface IApiKeyService
{
    /// <summary>Creates a new API key for the given owner.</summary>
    Task<ApiKeyCreatedResult> CreateAsync(Guid ownerId, IEnumerable<string> scopes, CancellationToken cancellationToken = default);

    /// <summary>Validates a raw API key and returns the associated owner, or null if invalid.</summary>
    Task<ApiKeyIdentity?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default);

    /// <summary>Rotates (revokes old + creates new) the API key with the given id.</summary>
    Task<ApiKeyCreatedResult> RotateAsync(Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>Revokes an API key.</summary>
    Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default);
}

/// <summary>Result returned when a new API key is created.</summary>
public sealed record ApiKeyCreatedResult(Guid KeyId, string RawKey);

/// <summary>Identity resolved from a valid API key.</summary>
public sealed record ApiKeyIdentity(Guid OwnerId, IReadOnlyList<string> Scopes);
