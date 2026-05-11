using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ai.Contracts;

namespace Ai.Application.Abstractions;

/// <summary>
/// Abstraction over a large-language-model provider (OpenAI, Anthropic, Azure OpenAI, Ollama).
/// Infrastructure implementations live in <c>Ai.Infrastructure.Providers</c>.
/// </summary>
public interface ILlmClient
{
    /// <summary>Performs a non-streaming chat completion.</summary>
    /// <param name="request">The chat request containing messages and model parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's reply together with usage statistics.</returns>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Performs a streaming chat completion, yielding tokens as they arrive.
    /// Suitable for Server-Sent Events (SSE) endpoints.
    /// </summary>
    /// <param name="request">The chat request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of token strings.</returns>
    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken ct = default);
}
