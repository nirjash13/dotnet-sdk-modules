namespace Ai.Contracts;

/// <summary>Request to generate a vector embedding for a piece of text.</summary>
public sealed class EmbeddingRequest
{
    /// <summary>Gets or sets the text to embed. Must not be null or empty.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional embedding model override.
    /// Null means use the configured default embedding model.
    /// </summary>
    public string? Model { get; set; }
}
