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
/// Email verification service.
/// Generates a cryptographically random token, stores its SHA-256 hash,
/// and (stub) logs the verification URL — real dispatch wires to Notifications module.
/// </summary>
public sealed class EmailVerificationService(
    IEmailVerificationTokenStore store,
    IUserRepository userRepository,
    ILogger<EmailVerificationService> logger)
    : IEmailVerificationService
{
    private const int TokenBytes = 32;
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public async Task SendVerificationEmailAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        byte[] raw = RandomNumberGenerator.GetBytes(TokenBytes);
        string rawToken = Convert.ToBase64String(raw);
        string tokenHash = HashToken(rawToken);

        var token = EmailVerificationToken.Create(
            id: Guid.NewGuid(),
            userId: userId,
            tokenHash: tokenHash,
            expiresAt: DateTimeOffset.UtcNow.Add(TokenExpiry));

        store.Add(token);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // SECURITY: Never log the raw token — it grants account-ownership. Log only the token id.
        // Phase 5 will dispatch a real email via Notifications module.
        logger.LogInformation(
            "Email verification token generated for user {UserId}. TokenId={TokenId}. " +
            "Wire Notifications module to deliver the email in production.",
            userId,
            token.Id);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawToken);

        string tokenHash = HashToken(rawToken);
        EmailVerificationToken? token = await store
            .FindByHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (token is null || !token.IsValid)
        {
            return false;
        }

        // FindByIdForUpdateAsync: tracked entity required so EF Core persists MarkEmailVerified.
        User? user = await userRepository
            .FindByIdForUpdateAsync(token.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return false;
        }

        token.MarkUsed();
        user.MarkEmailVerified();

        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Email verified for user {UserId}.", token.UserId);
        return true;
    }

    private static string HashToken(string rawToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
