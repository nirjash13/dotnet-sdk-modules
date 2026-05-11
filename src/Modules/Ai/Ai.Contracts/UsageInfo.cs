namespace Ai.Contracts;

/// <summary>Token and cost accounting for a single LLM request.</summary>
public sealed class UsageInfo
{
    /// <summary>Gets or sets the number of tokens consumed by the prompt (input).</summary>
    public int PromptTokens { get; set; }

    /// <summary>Gets or sets the number of tokens generated in the completion (output).</summary>
    public int CompletionTokens { get; set; }

    /// <summary>Gets or sets the total token count (prompt + completion).</summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost in USD for the request.
    /// Populated by <c>ILlmBudgetTracker</c> based on per-model pricing tables.
    /// </summary>
    public decimal CostUsd { get; set; }
}
