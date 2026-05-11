using FluentValidation;
using Gdpr.Application.Commands;

namespace Gdpr.Application.Validators;

/// <summary>Validates <see cref="RequestErasureCommand"/>.</summary>
public sealed class RequestErasureCommandValidator : AbstractValidator<RequestErasureCommand>
{
    /// <summary>Initializes a new instance of <see cref="RequestErasureCommandValidator"/>.</summary>
    public RequestErasureCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
