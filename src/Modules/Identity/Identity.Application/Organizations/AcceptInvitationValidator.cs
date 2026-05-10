using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="AcceptInvitationCommand"/>.</summary>
public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    /// <summary>Initializes a new instance of <see cref="AcceptInvitationValidator"/>.</summary>
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.RawToken).NotEmpty().WithMessage("Token is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
    }
}
