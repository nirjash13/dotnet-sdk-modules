using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="InviteMemberCommand"/>.</summary>
public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    /// <summary>Initializes a new instance of <see cref="InviteMemberValidator"/>.</summary>
    public InviteMemberValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(320).WithMessage("Email must not exceed 320 characters.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId is required.");
        RuleFor(x => x.InvitedByUserId).NotEmpty().WithMessage("InvitedByUserId is required.");
    }
}
