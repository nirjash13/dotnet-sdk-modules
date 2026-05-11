using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.ApiKeys;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.ApiKeys;

/// <summary>
/// API key service.
/// Raw keys use the format "sk_" + 32 URL-safe random bytes (base64url).
/// Keys are SHA-256 hashed before storage. The raw key is returned ONCE at creation.
/// </summary>
public sealed class ApiKeyService(
    IApiKeyStore store,
    ILogger<ApiKeyService> logger)
    : IApiKeyService
{
    private const int KeyBytes = 32;
    private const string KeyPrefix = "sk_";

    /// <inheritdoc />
    public async Task<ApiKeyCreatedResult> CreateAsync(
        Guid ownerId,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken = default)
    {
        string rawKey = GenerateRawKey();
        string keyHash = HashKey(rawKey);
        string displayPrefix = rawKey[..Math.Min(12, rawKey.Length)];
        List<string> scopesList = scopes.ToList();

        var apiKey = ApiKey.CreateUserScoped(
            id: Guid.NewGuid(),
            userId: ownerId,
            name: $"key-{displayPrefix}",
            keyPrefix: displayPrefix,
            keyHash: keyHash,
            scopes: scopesList);

        store.Add(apiKey);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("API key {KeyId} created for user {UserId}.", apiKey.Id, ownerId);
        return new ApiKeyCreatedResult(apiKey.Id, rawKey);
    }

    /// <inheritdoc />
    public async Task<ApiKeyIdentity?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawKey);

        string keyHash = HashKey(rawKey);
        ApiKey? apiKey = await store.FindByHashAsync(keyHash, cancellationToken).ConfigureAwait(false);

        if (apiKey is null || !apiKey.IsActive)
        {
            return null;
        }

        apiKey.RecordUsage();
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Guid ownerId = apiKey.UserId ?? apiKey.OrganizationId
            ?? throw new InvalidOperationException("API key has neither a UserId nor an OrganizationId.");

        List<string> scopes = System.Text.Json.JsonSerializer
            .Deserialize<List<string>>(apiKey.ScopesJson) ?? new List<string>();

        return new ApiKeyIdentity(ownerId, scopes);
    }

    /// <inheritdoc />
    public async Task<ApiKeyCreatedResult> RotateAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        ApiKey existing = await store.FindByIdAsync(keyId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"API key {keyId} not found.");

        // Revoke old key.
        existing.Revoke();

        // Parse existing scopes to preserve them on the new key.
        List<string> scopes = System.Text.Json.JsonSerializer
            .Deserialize<List<string>>(existing.ScopesJson) ?? new List<string>();

        // Create replacement — same owner, same scopes.
        Guid ownerId = existing.UserId ?? existing.OrganizationId
            ?? throw new InvalidOperationException("API key has neither a UserId nor an OrganizationId.");

        string rawKey = GenerateRawKey();
        string keyHash = HashKey(rawKey);
        string displayPrefix = rawKey[..Math.Min(12, rawKey.Length)];

        ApiKey newKey = existing.UserId.HasValue
            ? ApiKey.CreateUserScoped(Guid.NewGuid(), ownerId, $"key-{displayPrefix}", displayPrefix, keyHash, scopes)
            : ApiKey.CreateOrgScoped(Guid.NewGuid(), ownerId, $"key-{displayPrefix}", displayPrefix, keyHash, scopes);

        store.Add(newKey);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "API key {OldKeyId} rotated → {NewKeyId} for owner {OwnerId}.",
            keyId,
            newKey.Id,
            ownerId);

        return new ApiKeyCreatedResult(newKey.Id, rawKey);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        ApiKey? apiKey = await store.FindByIdAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (apiKey is null)
        {
            return;
        }

        apiKey.Revoke();
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("API key {KeyId} revoked.", keyId);
    }

    private static string GenerateRawKey()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(KeyBytes);

        // URL-safe base64 without padding.
        return KeyPrefix + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashKey(string rawKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToBase64String(bytes);
    }
}
