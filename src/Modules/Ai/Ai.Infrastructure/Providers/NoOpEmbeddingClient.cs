using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.Providers;

/// <summary>
/// Silent-degradation embedding client registered when no embedding provider is configured.
/// Returns a zero vector to allow the application to start without a provider key.
/// </summary>
internal sealed class NoOpEmbeddingClient : IEmbeddingClient
{
    private const int DefaultDimension = 1536;
    private readonly ILogger<NoOpEmbeddingClient> _logger;

    public NoOpEmbeddingClient(ILogger<NoOpEmbeddingClient> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpEmbeddingClient: embedding provider not configured — returning zero vector.");
        return Task.FromResult(Enumerable.Repeat(0f, DefaultDimension).ToArray());
    }
}
