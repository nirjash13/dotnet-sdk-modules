using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="RemoveMemberCommand"/>.</summary>
public sealed class RemoveMemberValidator : AbstractValidator<RemoveMemberCommand>
{
    /// <summary>Initializes a new instance of <see cref="RemoveMemberValidator"/>.</summary>
    public RemoveMemberValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.OwnerRoleId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
