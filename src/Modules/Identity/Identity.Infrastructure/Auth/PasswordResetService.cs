using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Auth;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Password reset service.
/// Tokens are 32-byte cryptographically random values, SHA-256 hashed at rest, single-use, 1h expiry.
/// </summary>
public sealed class PasswordResetService(
    IPasswordResetTokenStore store,
    IUserRepository userRepository,
    IArgon2idHasher hasher,
    ILogger<PasswordResetService> logger)
    : IPasswordResetService
{
    private const int TokenBytes = 32;
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public async Task InitiateAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        // Always return success to prevent user enumeration attacks.
        User? user = await userRepository
            .FindByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            logger.LogDebug("Password reset requested for unknown email {Email}. Ignoring silently.", email);
            return;
        }

        byte[] raw = RandomNumberGenerator.GetBytes(TokenBytes);
        string rawToken = Convert.ToBase64String(raw);
        string tokenHash = HashToken(rawToken);

        var token = PasswordResetToken.Create(
            id: Guid.NewGuid(),
            userId: user.Id,
            tokenHash: tokenHash,
            expiresAt: DateTimeOffset.UtcNow.Add(TokenExpiry));

        store.Add(token);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Stub: log reset link — Phase 5 dispatches a real email.
        logger.LogInformation(
            "Password reset token generated for user {UserId}. " +
            "Token (dev-only): {RawToken}.",
            user.Id,
            rawToken);
    }

    /// <inheritdoc />
    public async Task<bool> CompleteAsync(
        string rawToken,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawToken);
        ArgumentNullException.ThrowIfNull(newPassword);

        string tokenHash = HashToken(rawToken);
        PasswordResetToken? token = await store
            .FindByHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (token is null || !token.IsValid)
        {
            return false;
        }

        token.MarkUsed();
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The new password hash would normally be stored on the OpenIddict user record
        // or a dedicated credential table. For Phase 2 we log it (full password-change
        // flow requires deeper OpenIddict integration — deferred to Phase 3).
        string newHash = hasher.Hash(newPassword);
        logger.LogInformation(
            "Password reset completed for user {UserId}. New Argon2id hash starts with: {Prefix}...",
            token.UserId,
            newHash[..8]);

        return true;
    }

    private static string HashToken(string rawToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
