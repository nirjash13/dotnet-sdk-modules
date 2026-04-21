using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated, normalised email address.
/// </summary>
public sealed class EmailAddress : IEquatable<EmailAddress>
{
    private EmailAddress(string value)
    {
        Value = value;
    }

    /// <summary>Gets the normalised (trimmed, lower-cased) email string.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates an <see cref="EmailAddress"/> from a raw string.
    /// </summary>
    /// <param name="raw">The raw email string.</param>
    /// <returns>A validated, normalised <see cref="EmailAddress"/>.</returns>
    /// <exception cref="IdentityDomainException">Thrown when the value is null/whitespace or missing '@'.</exception>
    public static EmailAddress From(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new IdentityDomainException("Email address must not be empty.");
        }

        string normalised = raw.Trim().ToLowerInvariant();

        if (!normalised.Contains('@'))
        {
            throw new IdentityDomainException($"'{raw}' is not a valid email address.");
        }

        return new EmailAddress(normalised);
    }

    /// <inheritdoc />
    public bool Equals(EmailAddress? other) => other is not null && Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EmailAddress e && Equals(e);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Value;
}
