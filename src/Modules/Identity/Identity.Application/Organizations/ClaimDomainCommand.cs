using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command: org owner claims an email domain for auto-join.
/// Returns the verification token the org must publish as a DNS TXT record.
/// </summary>
/// <param name="OrganizationId">The organization claiming the domain.</param>
/// <param name="Domain">The domain to claim (e.g., "acme.com").</param>
/// <param name="RequestingUserId">The user making the claim (must be an org owner).</param>
public sealed record ClaimDomainCommand(
    Guid OrganizationId,
    string Domain,
    Guid RequestingUserId);

/// <summary>Result returned from <see cref="ClaimDomainHandler"/>.</summary>
/// <param name="ClaimId">The newly created claim identifier.</param>
/// <param name="VerificationToken">The DNS TXT token to publish under <c>_saasbuilder-verify.&lt;domain&gt;</c>.</param>
public sealed record ClaimDomainResult(Guid ClaimId, string VerificationToken);
