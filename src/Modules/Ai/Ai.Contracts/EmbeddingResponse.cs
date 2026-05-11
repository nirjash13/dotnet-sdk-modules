namespace Ai.Contracts;

/// <summary>Response containing the generated embedding vector.</summary>
public sealed class EmbeddingResponse
{
    /// <summary>Gets or sets the floating-point embedding vector.</summary>
    public float[] Embedding { get; set; } = System.Array.Empty<float>();

    /// <summary>Gets or sets the model that produced the embedding.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of tokens consumed by the embedding request.</summary>
    public int TokensUsed { get; set; }
}
