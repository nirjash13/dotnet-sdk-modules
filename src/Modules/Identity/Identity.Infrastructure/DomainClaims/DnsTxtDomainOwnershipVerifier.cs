using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;
using Identity.Application.Organizations;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.DomainClaims;

/// <summary>
/// Verifies domain ownership by resolving TXT records for <c>_saasbuilder-verify.&lt;domain&gt;</c>
/// using <c>DnsClient.NET</c> (TCP fallback enabled, configurable resolvers).
/// </summary>
/// <remarks>
/// This verifier uses the host's system resolvers (or operator-configured ones).
/// DNSSEC validation is NOT enforced by this implementation — if your deployment requires
/// DNSSEC, configure a DNSSEC-validating resolver (e.g., a local Unbound instance)
/// and this client will benefit from its AD-bit guarantees.
/// </remarks>
public sealed class DnsTxtDomainOwnershipVerifier : IDomainOwnershipVerifier
{
    private readonly ILookupClient _dns;
    private readonly ILogger<DnsTxtDomainOwnershipVerifier> _logger;

    /// <summary>Initializes a new instance of <see cref="DnsTxtDomainOwnershipVerifier"/>.</summary>
    public DnsTxtDomainOwnershipVerifier(ILogger<DnsTxtDomainOwnershipVerifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use the host's system resolvers; enable TCP fallback for truncated UDP responses.
        _dns = new LookupClient(new LookupClientOptions
        {
            UseTcpFallback = true,
            Retries = 2,
            Timeout = TimeSpan.FromSeconds(5),
            UseCache = false,
        });

        // C-14: DNSSEC AD-bit validation is not enforced. This verifier is not production-safe
        // without a DNSSEC-validating upstream resolver. AD-bit validation is tracked separately.
        _logger.LogWarning(
            "DnsTxtDomainOwnershipVerifier is operating without DNSSEC AD-bit validation. Not production-safe.");
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(
        string domain,
        string token,
        CancellationToken cancellationToken = default)
    {
        string verifyHost = $"_saasbuilder-verify.{domain}";
        string expectedValue = $"saasbuilder-verify={token}";

        try
        {
            IDnsQueryResponse response = await _dns
                .QueryAsync(verifyHost, QueryType.TXT, QueryClass.IN, cancellationToken)
                .ConfigureAwait(false);

            bool found = response.Answers
                .OfType<TxtRecord>()
                .SelectMany(r => r.Text)
                .Any(t => string.Equals(t, expectedValue, StringComparison.Ordinal));

            if (!found)
            {
                _logger.LogDebug(
                    "DNS TXT verification for {Host}: expected '{Expected}' — not found in {Count} TXT records.",
                    verifyHost,
                    expectedValue,
                    response.Answers.OfType<TxtRecord>().Count());
            }

            return found;
        }
        catch (DnsResponseException ex)
        {
            _logger.LogWarning(ex, "DNS TXT verification for {Host} failed: {DnsError}.", verifyHost, ex.Code);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DNS TXT verification for {Host} failed with exception.", verifyHost);
            return false;
        }
    }
}
