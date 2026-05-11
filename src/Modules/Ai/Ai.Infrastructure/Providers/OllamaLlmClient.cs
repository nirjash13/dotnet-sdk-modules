using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ai.Infrastructure.Providers;

/// <summary>
/// LLM client adapter for Ollama (local-dev, no auth).
/// Default endpoint: http://localhost:11434.
/// </summary>
internal sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaLlmClient> _logger;

    public OllamaLlmClient(
        HttpClient http,
        IOptions<OllamaOptions> options,
        ILogger<OllamaLlmClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        string model = request.Model ?? _options.Model;

        var messages = new List<object>();
        foreach (ChatMessage msg in request.Messages)
        {
            messages.Add(new { role = msg.Role.ToString().ToLowerInvariant(), content = msg.Content });
        }

        var payload = new { model, messages, stream = false };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(payload),
        };

        using HttpResponseMessage httpResponse = await _http
            .SendAsync(httpRequest, ct)
            .ConfigureAwait(false);

        httpResponse.EnsureSuccessStatusCode();

        using System.IO.Stream stream = await httpResponse.Content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);

        OllamaResponse? apiResponse = await JsonSerializer
            .DeserializeAsync<OllamaResponse>(stream, cancellationToken: ct)
            .ConfigureAwait(false);

        if (apiResponse is null)
        {
            throw new InvalidOperationException("Empty response from Ollama API.");
        }

        return new ChatResponse
        {
            Message = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = apiResponse.Message?.Content ?? string.Empty,
            },
            FinishReason = "stop",
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // GAP: Ollama supports native streaming. Implement line-by-line NDJSON parsing in Phase 10.x.
        ChatResponse response = await ChatAsync(request, ct).ConfigureAwait(false);
        yield return response.Message.Content;
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

/// <summary>Configuration options for the Ollama local provider.</summary>
public sealed class OllamaOptions
{
    /// <summary>Gets or sets the Ollama base URL. Defaults to http://localhost:11434.</summary>
    public string Endpoint { get; set; } = "http://localhost:11434/";

    /// <summary>Gets or sets the model to use (e.g. "llama3.2").</summary>
    public string Model { get; set; } = "llama3.2";
}
