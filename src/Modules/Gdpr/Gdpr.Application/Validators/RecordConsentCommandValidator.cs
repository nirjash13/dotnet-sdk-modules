using FluentValidation;
using Gdpr.Application.Commands;

namespace Gdpr.Application.Validators;

/// <summary>Validates <see cref="RecordConsentCommand"/>.</summary>
public sealed class RecordConsentCommandValidator : AbstractValidator<RecordConsentCommand>
{
    /// <summary>Initializes a new instance of <see cref="RecordConsentCommandValidator"/>.</summary>
    public RecordConsentCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ConsentKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(32);
    }
}
