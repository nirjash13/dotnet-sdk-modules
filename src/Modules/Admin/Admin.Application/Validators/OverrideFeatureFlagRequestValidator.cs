using FluentValidation;

namespace Admin.Application.Validators;

/// <summary>
/// Validates the feature-flag override request payload.
/// </summary>
public sealed class OverrideFeatureFlagRequest
{
    /// <summary>Gets or sets the boolean override value.</summary>
    public bool? Value { get; set; }

    /// <summary>Gets or sets the reason for the override.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// FluentValidation validator for <see cref="OverrideFeatureFlagRequest"/>.
/// </summary>
public sealed class OverrideFeatureFlagRequestValidator : AbstractValidator<OverrideFeatureFlagRequest>
{
    /// <summary>Initializes a new instance of <see cref="OverrideFeatureFlagRequestValidator"/>.</summary>
    public OverrideFeatureFlagRequestValidator()
    {
        RuleFor(r => r.Value)
            .NotNull().WithMessage("Value is required.");

        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(1024).WithMessage("Reason must not exceed 1024 characters.");
    }
}
