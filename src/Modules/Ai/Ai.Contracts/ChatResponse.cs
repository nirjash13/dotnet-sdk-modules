namespace Ai.Contracts;

/// <summary>Response returned by a non-streaming chat completion call.</summary>
public sealed class ChatResponse
{
    /// <summary>Gets or sets the assistant's reply message.</summary>
    public ChatMessage Message { get; set; } = new ChatMessage { Role = ChatRole.Assistant };

    /// <summary>Gets or sets token usage statistics for the request.</summary>
    public UsageInfo Usage { get; set; } = new UsageInfo();

    /// <summary>
    /// Gets or sets the reason the model stopped generating tokens.
    /// Common values: "stop", "length", "tool_calls", "content_filter".
    /// </summary>
    public string FinishReason { get; set; } = "stop";
}
