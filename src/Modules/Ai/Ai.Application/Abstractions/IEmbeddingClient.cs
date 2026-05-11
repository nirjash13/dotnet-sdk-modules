using System.Threading;
using System.Threading.Tasks;

namespace Ai.Application.Abstractions;

/// <summary>Abstraction for generating vector embeddings from text.</summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// Generates a normalised floating-point embedding vector for the supplied text.
    /// </summary>
    /// <param name="text">The text to embed. Must not be null or empty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A float array whose length matches the configured embedding model dimension.</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
