using FluentValidation;

namespace Admin.Application.Validators;

/// <summary>Request body for the refund action.</summary>
public sealed class RefundRequest
{
    /// <summary>Gets or sets the provider invoice identifier to refund.</summary>
    public string? InvoiceId { get; set; }

    /// <summary>Gets or sets the refund amount in cents.</summary>
    public long? AmountCents { get; set; }

    /// <summary>Gets or sets the reason for the refund.</summary>
    public string? Reason { get; set; }
}

/// <summary>FluentValidation validator for <see cref="RefundRequest"/>.</summary>
public sealed class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    /// <summary>Initializes a new instance of <see cref="RefundRequestValidator"/>.</summary>
    public RefundRequestValidator()
    {
        RuleFor(r => r.InvoiceId).NotEmpty().WithMessage("InvoiceId is required.");
        RuleFor(r => r.AmountCents).NotNull().GreaterThan(0).WithMessage("AmountCents must be positive.");
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(1024);
    }
}

/// <summary>Request body for the credit-grant action.</summary>
public sealed class CreditGrantRequest
{
    /// <summary>Gets or sets the credit amount in cents.</summary>
    public long? AmountCents { get; set; }

    /// <summary>Gets or sets the reason for the credit grant.</summary>
    public string? Reason { get; set; }
}

/// <summary>FluentValidation validator for <see cref="CreditGrantRequest"/>.</summary>
public sealed class CreditGrantRequestValidator : AbstractValidator<CreditGrantRequest>
{
    /// <summary>Initializes a new instance of <see cref="CreditGrantRequestValidator"/>.</summary>
    public CreditGrantRequestValidator()
    {
        RuleFor(r => r.AmountCents).NotNull().GreaterThan(0).WithMessage("AmountCents must be positive.");
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(1024);
    }
}

/// <summary>Request body for the impersonate action.</summary>
public sealed class ImpersonateRequest
{
    /// <summary>Gets or sets the target user identifier to impersonate.</summary>
    public System.Guid? UserId { get; set; }

    /// <summary>Gets or sets the reason for the impersonation session.</summary>
    public string? Reason { get; set; }
}

/// <summary>FluentValidation validator for <see cref="ImpersonateRequest"/>.</summary>
public sealed class ImpersonateRequestValidator : AbstractValidator<ImpersonateRequest>
{
    /// <summary>Initializes a new instance of <see cref="ImpersonateRequestValidator"/>.</summary>
    public ImpersonateRequestValidator()
    {
        RuleFor(r => r.UserId).NotNull().NotEmpty().WithMessage("UserId is required.");
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(1024);
    }
}
