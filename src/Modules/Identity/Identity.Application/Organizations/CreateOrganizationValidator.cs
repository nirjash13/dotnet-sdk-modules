using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="CreateOrganizationCommand"/>.</summary>
public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    /// <summary>Initializes a new instance of <see cref="CreateOrganizationValidator"/>.</summary>
    public CreateOrganizationValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(100).WithMessage("Slug must not exceed 100 characters.")
            .Matches(@"^[a-z0-9\-]+$").WithMessage("Slug may only contain lowercase letters, digits, and hyphens.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.");
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("OwnerUserId is required.");
        RuleFor(x => x.OwnerRoleId).NotEmpty().WithMessage("OwnerRoleId is required.");
    }
}
