using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="TransferOwnershipCommand"/>.</summary>
public sealed class TransferOwnershipValidator : AbstractValidator<TransferOwnershipCommand>
{
    /// <summary>Initializes a new instance of <see cref="TransferOwnershipValidator"/>.</summary>
    public TransferOwnershipValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.NewOwnerUserId).NotEmpty();
        RuleFor(x => x.CurrentOwnerUserId).NotEmpty();
        RuleFor(x => x.OwnerRoleId).NotEmpty();
        RuleFor(x => x.NewOwnerUserId)
            .NotEqual(x => x.CurrentOwnerUserId)
            .WithMessage("New owner must be a different user than the current owner.");
    }
}
