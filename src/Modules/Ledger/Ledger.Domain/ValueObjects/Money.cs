using System;
using Ledger.Domain.Exceptions;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount with a currency.
/// Stored by EF Core as two columns: <c>amount numeric(19,4)</c> and <c>currency char(3)</c>.
/// </summary>
/// <remarks>
/// Implemented as a sealed class (not a record) to:
/// 1. Control equality semantics explicitly.
/// 2. Avoid positional parameter StyleCop SA1313 warnings.
/// 3. Maintain consistent pattern with other value objects in this solution.
/// </remarks>
public sealed class Money : IEquatable<Money>
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the monetary amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code (e.g. "USD", "EUR", "GBP").</summary>
    public string Currency { get; }

    /// <summary>Value-equality operator.</summary>
    public static bool operator ==(Money? left, Money? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Value-inequality operator.</summary>
    public static bool operator !=(Money? left, Money? right) => !(left == right);

    /// <summary>
    /// Creates a new <see cref="Money"/> instance after validating the currency code.
    /// </summary>
    /// <param name="amount">The monetary amount. May be negative (credits/debits).</param>
    /// <param name="currency">A 3-character ISO 4217 currency code. Must be exactly 3 characters.</param>
    /// <returns>A new <see cref="Money"/> instance.</returns>
    /// <exception cref="LedgerDomainException">Thrown when <paramref name="currency"/> is not exactly 3 characters.</exception>
    public static Money From(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new LedgerDomainException("Currency must not be null or empty.");
        }

        if (currency.Length != 3)
        {
            throw new LedgerDomainException(
                $"Currency code must be exactly 3 characters (ISO 4217). Got: '{currency}' ({currency.Length} chars).");
        }

        return new Money(amount, currency.ToUpperInvariant());
    }

    /// <inheritdoc />
    public bool Equals(Money? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Amount == other.Amount
            && string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Money);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Amount, Currency.ToUpperInvariant());

    /// <inheritdoc />
    public override string ToString() => $"{Amount:F4} {Currency}";
}
