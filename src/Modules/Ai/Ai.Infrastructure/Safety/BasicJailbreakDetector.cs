using System;
using System.Text.RegularExpressions;

namespace Ai.Infrastructure.Safety;

/// <summary>
/// Token-level jailbreak heuristic. Scores a prompt [0, 1] based on presence of
/// known jailbreak phrase patterns.
/// <para>
/// This is a lightweight first-pass filter — it is not a replacement for a trained
/// classifier. High false-negative rate on novel jailbreaks. Use as one signal
/// alongside rate limiting and user reputation scoring.
/// </para>
/// </summary>
internal static class BasicJailbreakDetector
{
    private static readonly (Regex Pattern, double Weight)[] Rules = new[]
    {
        (Build(@"ignore\s+(all\s+)?(previous|above|prior)\s+instructions?"), 0.9),
        (Build(@"you\s+are\s+now\s+(in\s+)?DAN\b"), 0.9),
        (Build(@"pretend\s+(you\s+are|to\s+be)\s+(an?\s+)?AI\s+without\s+restrictions?"), 0.85),
        (Build(@"jailbreak"), 0.7),
        (Build(@"do\s+anything\s+now"), 0.75),
        (Build(@"act\s+as\s+if\s+you\s+have\s+no\s+(ethical\s+)?(guidelines?|restrictions?)"), 0.85),
        (Build(@"system\s*:\s*you\s+are"), 0.5),
        (Build(@"simulate\s+(evil|harmful|unethical)"), 0.8),
    };

    /// <summary>
    /// Computes a jailbreak confidence score for the given text.
    /// Returns the maximum weight of any matching rule, or 0.0 if none match.
    /// </summary>
    public static double ComputeScore(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        double maxScore = 0.0;
        foreach ((Regex pattern, double weight) in Rules)
        {
            if (pattern.IsMatch(text))
            {
                maxScore = Math.Max(maxScore, weight);
            }
        }

        return maxScore;
    }

    private static Regex Build(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
}
