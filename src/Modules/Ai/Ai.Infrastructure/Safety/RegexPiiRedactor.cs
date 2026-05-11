using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;

namespace Ai.Infrastructure.Safety;

/// <summary>
/// PII redactor using compiled regular expressions to detect and replace:
/// email addresses, SSN-like patterns, credit card numbers, and US phone numbers.
/// Replacements use the format <c>[REDACTED:TYPE]</c>.
/// </summary>
public sealed class RegexPiiRedactor : IPromptSafetyFilter
{
    // Email: simple but high-recall pattern.
    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    // SSN: 9 digits with optional separators (dashes or spaces).
    private static readonly Regex SsnRegex = new(
        @"\b(?!000|666|9\d{2})\d{3}[- ]?(?!00)\d{2}[- ]?(?!0000)\d{4}\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    // Credit card: 13–19 digits with optional spaces or dashes between groups.
    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d[ \-]?){13,19}\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    // US phone: various formats including international prefix.
    private static readonly Regex PhoneRegex = new(
        @"\b(?:\+?1[\s.-]?)?\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <inheritdoc/>
    public Task<string> RedactPiiAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(text);
        }

        // Order matters: run SSN before credit card (SSN is a subset of digit sequences).
        string result = EmailRegex.Replace(text, "[REDACTED:EMAIL]");
        result = SsnRegex.Replace(result, "[REDACTED:SSN]");
        result = CreditCardRegex.Replace(result, "[REDACTED:CREDITCARD]");
        result = PhoneRegex.Replace(result, "[REDACTED:PHONE]");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<double> DetectJailbreakAsync(string text, CancellationToken ct = default)
    {
        return Task.FromResult(BasicJailbreakDetector.ComputeScore(text));
    }
}
