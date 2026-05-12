using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Impersonation;
using Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Impersonation;

/// <summary>
/// Safe impersonation service (per Pigment pattern).
/// Produces a short-lived JWT with an <c>act</c> claim identifying the original admin,
/// a <c>X-Impersonation: true</c> claim, and a 1-hour hard ceiling on session duration.
/// Full structured audit log on every start/end action.
/// </summary>
public sealed class ImpersonationService(
    IImpersonationSessionStore store,
    IConfiguration configuration,
    ILogger<ImpersonationService> logger)
    : IImpersonationService
{
    private static readonly TimeSpan MaxSessionDuration = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public async Task<ImpersonationSession> StartAsync(
        Guid adminUserId,
        Guid targetUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(MaxSessionDuration);
        string token = MintImpersonationToken(adminUserId, targetUserId, expiresAt);

        var session = ImpersonationSessionEntity.Create(
            id: Guid.NewGuid(),
            adminUserId: adminUserId,
            targetUserId: targetUserId,
            reason: reason,
            impersonationToken: token,
            expiresAt: expiresAt);

        store.Add(session);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Structured audit log — every impersonation start is audited.
        logger.LogWarning(
            "IMPERSONATION_START SessionId={SessionId} Admin={AdminUserId} Target={TargetUserId} " +
            "Reason={Reason} ExpiresAt={ExpiresAt}",
            session.Id,
            adminUserId,
            targetUserId,
            reason,
            expiresAt);

        return new ImpersonationSession(session.Id, token, expiresAt);
    }

    /// <inheritdoc />
    public async Task EndAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        ImpersonationSessionEntity? session = await store
            .FindByIdAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return;
        }

        session.End();
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogWarning(
            "IMPERSONATION_END SessionId={SessionId} Admin={AdminUserId} Target={TargetUserId}",
            sessionId,
            session.AdminUserId,
            session.TargetUserId);
    }

    private string MintImpersonationToken(
        Guid adminUserId,
        Guid targetUserId,
        DateTimeOffset expiresAt)
    {
        // SECURITY: Require explicit signing key configuration — no fallback constant allowed.
        // The same known-value secret would be shipped to every SDK consumer.
        string? signingKeyValue = configuration["Identity:ImpersonationSigningKey"];
        if (string.IsNullOrWhiteSpace(signingKeyValue))
        {
            throw new InvalidOperationException(
                "Identity:ImpersonationSigningKey must be configured. " +
                "Set it to at least 32 cryptographically random bytes (base64-encoded) " +
                "via an environment variable or secret store. " +
                "A hardcoded fallback is intentionally absent — every SDK consumer must provide their own key.");
        }

        // The key must be at least 32 bytes (256 bits) for HS256.
        byte[] keyBytes = Convert.FromBase64String(signingKeyValue);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Identity:ImpersonationSigningKey must be at least 32 bytes (256 bits) when base64-decoded. " +
                $"Provided key decodes to {keyBytes.Length} bytes.");
        }

        string issuer = configuration["Identity:Issuer"] ?? "saasbuilder";
        const string Audience = "saasbuilder:impersonation";

        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", targetUserId.ToString()),
            new Claim("act", adminUserId.ToString()),  // RFC 8693 actor claim
            new Claim("impersonation", "true"),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = Audience,
            NotBefore = DateTime.UtcNow,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(tokenDescriptor);
    }
}
