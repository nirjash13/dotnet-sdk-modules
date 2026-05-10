using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="ChangeMemberRoleCommand"/>.</summary>
public sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    /// <summary>Initializes a new instance of <see cref="ChangeMemberRoleValidator"/>.</summary>
    public ChangeMemberRoleValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.NewRoleId).NotEmpty();
        RuleFor(x => x.OwnerRoleId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
