using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Organizations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.DomainClaims;

/// <summary>
/// Development-only verifier that auto-approves all domain claims when
/// <c>Identity:DomainClaims:AutoVerify=true</c> is set.
/// Never register this in production.
/// </summary>
public sealed class NoopDomainOwnershipVerifier : IDomainOwnershipVerifier
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NoopDomainOwnershipVerifier> _logger;

    /// <summary>Initializes a new instance of <see cref="NoopDomainOwnershipVerifier"/>.</summary>
    public NoopDomainOwnershipVerifier(
        IConfiguration configuration,
        ILogger<NoopDomainOwnershipVerifier> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // S2 (C-15): emit a startup warning so operators know verification is disabled.
        _logger.LogWarning(
            "Domain ownership verification is DISABLED (Noop verifier). " +
            "Set Identity:DomainVerification:Provider=Dns for production.");
    }

    /// <inheritdoc />
    public Task<bool> VerifyAsync(
        string domain,
        string token,
        CancellationToken cancellationToken = default)
    {
        bool autoVerify = _configuration.GetValue<bool>("Identity:DomainClaims:AutoVerify");
        if (autoVerify)
        {
            _logger.LogWarning(
                "NoopDomainOwnershipVerifier: auto-approving domain '{Domain}' (Identity:DomainClaims:AutoVerify=true).",
                domain);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
