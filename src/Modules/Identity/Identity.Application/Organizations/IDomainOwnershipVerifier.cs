using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Organizations;

/// <summary>
/// Verifies that a domain is owned by checking the expected DNS TXT record.
/// </summary>
public interface IDomainOwnershipVerifier
{
    /// <summary>
    /// Returns <see langword="true"/> if the DNS TXT record
    /// <c>_saasbuilder-verify.&lt;domain&gt;</c> contains the expected <paramref name="token"/> value.
    /// </summary>
    Task<bool> VerifyAsync(string domain, string token, CancellationToken cancellationToken = default);
}
