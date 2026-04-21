using FluentValidation;
using Ledger.Application.Commands;

namespace Ledger.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="PostTransactionCommand"/>.
/// Registered via <c>AddValidatorsFromAssembly</c> in <c>LedgerModule.ConfigureServices</c>.
/// </summary>
public sealed class PostTransactionCommandValidator : AbstractValidator<PostTransactionCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public PostTransactionCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("AccountId must not be empty.");

        RuleFor(x => x.Amount)
            .NotEqual(0m)
            .WithMessage("Amount must not be zero.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency must not be empty.")
            .Length(3)
            .WithMessage("Currency must be exactly 3 characters (ISO 4217).");
    }
}
