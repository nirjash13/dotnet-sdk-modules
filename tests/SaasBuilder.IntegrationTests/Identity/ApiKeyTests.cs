using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Application.ApiKeys;
using Identity.Domain.Entities;
using Identity.Infrastructure.ApiKeys;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing tests for <see cref="IApiKeyService"/> behavior.
///
/// Load-bearing rationale:
/// 1. Create returns a raw key that starts with "sk_" and is never stored plain-text.
///    If the hash is not computed or the prefix is missing, this key cannot authenticate.
/// 2. Validate with the correct raw key returns the owner identity.
///    If the hash comparison is wrong, every API key auth fails.
/// 3. Revoked key cannot be validated.
///    If revocation is not checked, a revoked key continues to authenticate — security failure.
/// </summary>
public sealed class ApiKeyTests
{
    private static readonly Guid OwnerId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

    // ── Test 1: Creation returns a raw key with correct format ────────────────

    [Fact]
    public async Task CreateApiKey_ReturnsRawKeyWithSkPrefix()
    {
        // Arrange
        var storeMock = new Mock<IApiKeyStore>();
        ApiKey? stored = null;
        storeMock.Setup(s => s.Add(It.IsAny<ApiKey>()))
            .Callback<ApiKey>(k => stored = k);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = new ApiKeyService(storeMock.Object, NullLogger<ApiKeyService>.Instance);

        // Act
        ApiKeyCreatedResult result = await service.CreateAsync(OwnerId, new[] { "read" }, default);

        // Assert
        result.RawKey.Should().StartWith("sk_", "all API keys must use the 'sk_' prefix");
        result.KeyId.Should().NotBe(Guid.Empty);
        stored.Should().NotBeNull("the key must be persisted");
        stored!.KeyHash.Should().NotBe(result.RawKey, "raw key must not be stored plain-text");
        stored.IsActive.Should().BeTrue();
    }

    // ── Test 2: Validate with correct key returns owner identity ──────────────

    [Fact]
    public async Task ValidateApiKey_WithCorrectRawKey_ReturnsOwnerIdentity()
    {
        // Arrange — create a key first to get consistent hash.
        var storeMock = new Mock<IApiKeyStore>();
        ApiKey? storedKey = null;
        storeMock.Setup(s => s.Add(It.IsAny<ApiKey>()))
            .Callback<ApiKey>(k => storedKey = k);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = new ApiKeyService(storeMock.Object, NullLogger<ApiKeyService>.Instance);
        ApiKeyCreatedResult created = await service.CreateAsync(OwnerId, new[] { "read" }, default);

        // Wire up FindByHash to return the stored key.
        storeMock.Setup(s => s.FindByHashAsync(storedKey!.KeyHash, default)).ReturnsAsync(storedKey);

        // Act
        ApiKeyIdentity? identity = await service.ValidateAsync(created.RawKey, default);

        // Assert
        identity.Should().NotBeNull("a valid raw key must resolve to an identity");
        identity!.OwnerId.Should().Be(OwnerId);
        identity.Scopes.Should().Contain("read");
    }

    // ── Test 3: Revoked key cannot be validated ───────────────────────────────

    [Fact]
    public async Task ValidateApiKey_WithRevokedKey_ReturnsNull()
    {
        // Arrange — create and immediately revoke.
        var storeMock = new Mock<IApiKeyStore>();
        ApiKey? storedKey = null;
        storeMock.Setup(s => s.Add(It.IsAny<ApiKey>()))
            .Callback<ApiKey>(k => storedKey = k);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = new ApiKeyService(storeMock.Object, NullLogger<ApiKeyService>.Instance);
        ApiKeyCreatedResult created = await service.CreateAsync(OwnerId, new[] { "write" }, default);

        // Revoke the key.
        storedKey!.Revoke();
        storeMock.Setup(s => s.FindByHashAsync(storedKey.KeyHash, default)).ReturnsAsync(storedKey);

        // Act
        ApiKeyIdentity? identity = await service.ValidateAsync(created.RawKey, default);

        // Assert
        identity.Should().BeNull("a revoked key must not authenticate");
    }
}
