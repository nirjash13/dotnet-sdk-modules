using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Organizations;

/// <summary>
/// Represents an email domain claimed by an <see cref="Organization"/>.
/// Users registering with a verified email in this domain are auto-joined to the org.
/// </summary>
public sealed class OrganizationDomainClaim
{
    private OrganizationDomainClaim()
    {
    }

    /// <summary>Gets the claim identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the owning organization identifier.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the lowercased claimed domain (e.g., "acme.com").</summary>
    public string Domain { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the DNS TXT challenge token. Null after verification is complete.
    /// The expected TXT record value is: <c>saasbuilder-verify=&lt;VerificationToken&gt;</c>.
    /// </summary>
    public string? VerificationToken { get; private set; }

    /// <summary>Gets the UTC timestamp when the domain was successfully verified, or null if still pending.</summary>
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when the claim was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets a value indicating whether this claim has been verified.</summary>
    public bool IsVerified => VerifiedAt.HasValue;

    /// <summary>
    /// Creates a new pending <see cref="OrganizationDomainClaim"/> with a generated verification token.
    /// </summary>
    public static OrganizationDomainClaim Create(Guid organizationId, string domain)
    {
        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("OrganizationId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new IdentityDomainException("Domain must not be empty.");
        }

        return new OrganizationDomainClaim
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Domain = domain.Trim().ToLowerInvariant(),
            VerificationToken = GenerateVerificationToken(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Marks the domain claim as verified.</summary>
    public void MarkVerified()
    {
        if (IsVerified)
        {
            return;
        }

        VerifiedAt = DateTimeOffset.UtcNow;

        // Keep token so callers can still read it for display; they can delete the record if they want to remove.
    }

    private static string GenerateVerificationToken()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);
}
