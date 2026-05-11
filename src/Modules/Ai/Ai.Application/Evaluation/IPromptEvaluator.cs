using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ai.Application.Evaluation;

/// <summary>Runs a golden-set evaluation against the configured LLM pipeline.</summary>
public interface IPromptEvaluator
{
    /// <summary>
    /// Executes all evaluation cases from the golden-set YAML/JSON files and returns
    /// per-case pass/fail results.
    /// </summary>
    Task<IReadOnlyList<EvalResult>> RunAsync(CancellationToken ct = default);
}

/// <summary>Result of evaluating a single golden-set test case.</summary>
public sealed class EvalResult
{
    /// <summary>Gets or sets the name of the evaluation case.</summary>
    public string CaseName { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the case passed.</summary>
    public bool Passed { get; set; }

    /// <summary>Gets or sets the actual model output.</summary>
    public string ActualOutput { get; set; } = string.Empty;

    /// <summary>Gets or sets the expected output pattern or value.</summary>
    public string ExpectedPattern { get; set; } = string.Empty;

    /// <summary>Gets or sets a human-readable failure reason, or null on pass.</summary>
    public string? FailureReason { get; set; }
}
