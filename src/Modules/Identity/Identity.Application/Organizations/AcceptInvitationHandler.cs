using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="AcceptInvitationCommand"/>.
/// Hashes the provided raw token, finds the matching invitation, marks it redeemed,
/// and creates an active <see cref="Member"/> for the accepting user.
/// </summary>
public sealed class AcceptInvitationHandler
{
    private readonly IInvitationRepository _invitations;
    private readonly IMemberRepository _members;

    /// <summary>Initializes a new instance of <see cref="AcceptInvitationHandler"/>.</summary>
    public AcceptInvitationHandler(IInvitationRepository invitations, IMemberRepository members)
    {
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _members = members ?? throw new ArgumentNullException(nameof(members));
    }

    /// <summary>
    /// Executes the command. Returns failure when the token is invalid, expired, or already redeemed.
    /// </summary>
    public async Task<Result<AcceptInvitationResult>> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string tokenHash = HashToken(command.RawToken);

        Invitation? invitation = await _invitations
            .FindByTokenHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null || !invitation.IsActive)
        {
            // Return generic message to avoid token enumeration.
            return Result<AcceptInvitationResult>.Failure("Invitation is invalid or has expired.");
        }

        invitation.Redeem();

        Member member = Member.CreateActive(
            id: Guid.NewGuid(),
            organizationId: invitation.OrganizationId,
            userId: command.UserId,
            roleId: invitation.RoleId);

        _members.Add(member);
        await _invitations.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<AcceptInvitationResult>.Success(
            new AcceptInvitationResult(invitation.OrganizationId, member.Id));
    }

    private static string HashToken(string rawToken)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
