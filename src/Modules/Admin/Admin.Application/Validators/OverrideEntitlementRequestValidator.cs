using FluentValidation;

namespace Admin.Application.Validators;

/// <summary>
/// Validates the entitlement override request payload.
/// </summary>
public sealed class OverrideEntitlementRequest
{
    /// <summary>Gets or sets the entitlement value (boolean or numeric string).</summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets the reason for the override.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// FluentValidation validator for <see cref="OverrideEntitlementRequest"/>.
/// </summary>
public sealed class OverrideEntitlementRequestValidator : AbstractValidator<OverrideEntitlementRequest>
{
    /// <summary>Initializes a new instance of <see cref="OverrideEntitlementRequestValidator"/>.</summary>
    public OverrideEntitlementRequestValidator()
    {
        RuleFor(r => r.Value)
            .NotEmpty().WithMessage("Value is required.");

        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(1024).WithMessage("Reason must not exceed 1024 characters.");
    }
}
