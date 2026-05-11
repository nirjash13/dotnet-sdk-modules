using FluentValidation;

namespace Identity.Application.Organizations;

/// <summary>FluentValidation validator for <see cref="ClaimDomainCommand"/>.</summary>
public sealed class ClaimDomainValidator : AbstractValidator<ClaimDomainCommand>
{
    /// <summary>Initializes a new instance of <see cref="ClaimDomainValidator"/>.</summary>
    public ClaimDomainValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
        RuleFor(x => x.Domain)
            .NotEmpty()
            .MaximumLength(253)
            .Matches(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$")
            .WithMessage("Domain must be a valid fully-qualified domain name (e.g., acme.com).");
    }
}
