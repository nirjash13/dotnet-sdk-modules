using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="InviteMemberCommand"/>.
/// Generates a cryptographically-random invitation token, stores its SHA-256 hash,
/// persists the invitation, and dispatches a MemberInvited integration event.
/// </summary>
public sealed class InviteMemberHandler
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);

    private readonly IOrganizationRepository _organizations;
    private readonly IInvitationRepository _invitations;

    /// <summary>Initializes a new instance of <see cref="InviteMemberHandler"/>.</summary>
    public InviteMemberHandler(
        IOrganizationRepository organizations,
        IInvitationRepository invitations)
    {
        _organizations = organizations ?? throw new ArgumentNullException(nameof(organizations));
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
    }

    /// <summary>Executes the command. Returns failure if the organization does not exist.</summary>
    public async Task<Result<InviteMemberResult>> HandleAsync(
        InviteMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Organization? org = await _organizations
            .FindByIdAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (org is null)
        {
            return Result<InviteMemberResult>.Failure($"Organization '{command.OrganizationId}' not found.");
        }

        // Generate a cryptographically random URL-safe token.
        string rawToken = GenerateToken();
        string tokenHash = HashToken(rawToken);

        Invitation invitation = Invitation.Create(
            id: Guid.NewGuid(),
            organizationId: command.OrganizationId,
            email: command.Email,
            roleId: command.RoleId,
            tokenHash: tokenHash,
            expiresAt: DateTimeOffset.UtcNow.Add(DefaultExpiry),
            createdById: command.InvitedByUserId);

        _invitations.Add(invitation);
        await _invitations.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // TODO(Phase 2 — implementation): dispatch MemberInvited CloudEvent via IModuleDispatcher
        // so that the Notifications module sends the invitation email.

        return Result<InviteMemberResult>.Success(new InviteMemberResult(invitation.Id, rawToken));
    }

    private static string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string HashToken(string rawToken)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
