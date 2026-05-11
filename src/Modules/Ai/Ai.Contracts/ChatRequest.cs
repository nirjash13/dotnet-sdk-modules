using System.Collections.Generic;

namespace Ai.Contracts;

/// <summary>Request body for a non-streaming chat completion.</summary>
public sealed class ChatRequest
{
    /// <summary>Gets or sets the ordered conversation history including the user's latest message.</summary>
    public IList<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>
    /// Gets or sets the model identifier (e.g. "gpt-4o", "claude-3-5-sonnet-20241022").
    /// Null means use the configured default model for the active provider.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the maximum number of tokens to generate. Null uses the provider default.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the sampling temperature in the range [0, 2].
    /// Higher values produce more random output. Null uses the provider default.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the tool/function definitions available to the model.</summary>
    public IList<ToolDefinition>? Tools { get; set; }
}

/// <summary>Schema for a tool the model may choose to call.</summary>
public sealed class ToolDefinition
{
    /// <summary>Gets or sets the unique tool name (snake_case recommended).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable description surfaced to the model.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON Schema string describing the tool's parameters.</summary>
    public string ParametersSchemaJson { get; set; } = "{}";
}
