using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.ApiKeys;

/// <summary>
/// Abstraction for Machine-to-Machine (M2M) OAuth client_credentials token issuance.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): OAuth2 client_credentials flow with per-app scopes.
/// Authorization handlers accept JWT, API key, or M2M token interchangeably.
/// </remarks>
public interface IM2MTokenService
{
    /// <summary>Issues an M2M access token for the given client application.</summary>
    Task<M2MTokenResult> IssueTokenAsync(
        string clientId,
        string clientSecret,
        IEnumerable<string> requestedScopes,
        CancellationToken cancellationToken = default);
}

/// <summary>Issued M2M access token.</summary>
public sealed record M2MTokenResult(string AccessToken, DateTimeOffset ExpiresAt, IReadOnlyList<string> GrantedScopes);
