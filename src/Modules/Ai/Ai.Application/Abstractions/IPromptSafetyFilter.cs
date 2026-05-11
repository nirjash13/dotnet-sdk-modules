using System.Threading;
using System.Threading.Tasks;

namespace Ai.Application.Abstractions;

/// <summary>
/// Safety filter applied to prompts before they are sent to an LLM provider.
/// Implementations run PII redaction and jailbreak detection.
/// </summary>
public interface IPromptSafetyFilter
{
    /// <summary>
    /// Redacts personally-identifiable information from <paramref name="text"/>,
    /// replacing matches with tokens like <c>[REDACTED:EMAIL]</c>.
    /// </summary>
    /// <param name="text">Raw user input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sanitised text with PII replaced.</returns>
    Task<string> RedactPiiAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Detects prompt-injection / jailbreak attempts in <paramref name="text"/>.
    /// </summary>
    /// <param name="text">Raw user input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A confidence score in the range [0, 1] where 0 means definitely safe and
    /// 1 means almost certainly a jailbreak attempt.
    /// Callers should reject or flag inputs above a configured threshold (default 0.8).
    /// </returns>
    Task<double> DetectJailbreakAsync(string text, CancellationToken ct = default);
}
