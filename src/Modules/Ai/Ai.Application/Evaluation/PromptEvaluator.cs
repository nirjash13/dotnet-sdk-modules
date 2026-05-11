using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Microsoft.Extensions.Logging;

namespace Ai.Application.Evaluation;

/// <summary>
/// Golden-set evaluator that runs a fixed set of in-code test cases.
/// <para>
/// GAP: Production implementations should load cases from YAML/JSON files
/// on disk so they can be updated without recompilation. The file-based loader
/// is deferred — this in-code version validates the interface and wiring.
/// </para>
/// </summary>
public sealed class PromptEvaluator : IPromptEvaluator
{
    private readonly ILlmClient _llm;
    private readonly ILogger<PromptEvaluator> _logger;

    /// <summary>Initializes a new instance of <see cref="PromptEvaluator"/>.</summary>
    public PromptEvaluator(ILlmClient llm, ILogger<PromptEvaluator> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EvalResult>> RunAsync(CancellationToken ct = default)
    {
        // In-code golden set. Replace with YAML/JSON file loader in Phase 10.x.
        var cases = new[]
        {
            new EvalCase(
                "basic_greeting",
                "Say hello in one word.",
                @"(?i)\bhello\b|\bhi\b|\bhey\b"),
        };

        var results = new List<EvalResult>(cases.Length);

        foreach (var evalCase in cases)
        {
            var result = await RunCaseAsync(evalCase, ct).ConfigureAwait(false);
            results.Add(result);
            _logger.LogInformation(
                "Eval case '{Case}': {Status}",
                evalCase.Name,
                result.Passed ? "PASS" : "FAIL");
        }

        return results;
    }

    private async Task<EvalResult> RunCaseAsync(EvalCase evalCase, CancellationToken ct)
    {
        try
        {
            var request = new ChatRequest
            {
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatRole.User, Content = evalCase.Prompt },
                },
            };

            ChatResponse response = await _llm.ChatAsync(request, ct).ConfigureAwait(false);
            string actual = response.Message.Content;
            bool passed = Regex.IsMatch(actual, evalCase.ExpectedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

            return new EvalResult
            {
                CaseName = evalCase.Name,
                Passed = passed,
                ActualOutput = actual,
                ExpectedPattern = evalCase.ExpectedPattern,
                FailureReason = passed ? null : $"Output '{actual}' did not match pattern '{evalCase.ExpectedPattern}'.",
            };
        }
        catch (Exception ex)
        {
            return new EvalResult
            {
                CaseName = evalCase.Name,
                Passed = false,
                ActualOutput = string.Empty,
                ExpectedPattern = evalCase.ExpectedPattern,
                FailureReason = $"Exception: {ex.Message}",
            };
        }
    }

    private sealed record EvalCase(string Name, string Prompt, string ExpectedPattern);
}
