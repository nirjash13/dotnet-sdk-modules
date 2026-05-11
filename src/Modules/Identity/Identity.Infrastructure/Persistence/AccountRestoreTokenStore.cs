using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Lifecycle;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAccountRestoreTokenStore"/>.
/// Raw tokens are hashed with SHA-256 before storage.
/// </summary>
internal sealed class AccountRestoreTokenStore(IdentityDbContext db) : IAccountRestoreTokenStore
{
    /// <inheritdoc />
    public async Task<string> CreateAsync(
        Guid userId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        // Generate a cryptographically random 32-byte (256-bit) token.
        byte[] rawBytes = RandomNumberGenerator.GetBytes(32);
        string rawToken = Convert.ToBase64String(rawBytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);

        string tokenHash = HashToken(rawToken);

        AccountRestoreToken entity = AccountRestoreToken.Create(userId, tokenHash, expiresAt);
        db.AccountRestoreTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return rawToken;
    }

    /// <inheritdoc />
    public async Task<AccountRestoreTokenEntry?> ConsumeAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        string tokenHash = HashToken(rawToken);

        AccountRestoreToken? entity = await db.AccountRestoreTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash
                     && t.UsedAt == null
                     && t.ExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        entity.MarkUsed();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AccountRestoreTokenEntry(entity.UserId, entity.ExpiresAt);
    }

    private static string HashToken(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
