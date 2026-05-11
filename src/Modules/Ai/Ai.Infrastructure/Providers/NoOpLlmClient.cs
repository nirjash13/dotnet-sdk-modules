using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.Providers;

/// <summary>
/// Silent-degradation LLM client registered when no provider API key is configured.
/// Returns a human-readable error message instead of throwing, satisfying the
/// <see cref="SaasBuilder.SharedKernel"/> silent-degradation principle.
/// </summary>
internal sealed class NoOpLlmClient : ILlmClient
{
    private const string NotConfiguredMessage = "AI provider is not configured. Set Ai:Provider and the corresponding API key.";
    private readonly ILogger<NoOpLlmClient> _logger;

    public NoOpLlmClient(ILogger<NoOpLlmClient> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpLlmClient: AI provider not configured — returning stub response.");
        var response = new ChatResponse
        {
            Message = new ChatMessage { Role = ChatRole.Assistant, Content = NotConfiguredMessage },
            FinishReason = "stop",
        };
        return Task.FromResult(response);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpLlmClient: AI provider not configured — yielding stub token.");
        await Task.CompletedTask.ConfigureAwait(false);
        yield return NotConfiguredMessage;
    }
}
