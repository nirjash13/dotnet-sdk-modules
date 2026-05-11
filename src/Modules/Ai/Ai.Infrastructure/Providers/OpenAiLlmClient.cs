using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
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
/// LLM client adapter for OpenAI (api.openai.com) and compatible endpoints.
/// Uses <see cref="HttpClient"/> directly rather than the <c>Azure.AI.OpenAI</c> SDK
/// to avoid a transitive dependency on the Azure SDK.
/// <para>
/// GAP: Streaming via the native SSE wire-protocol requires parsing "data: " lines from
/// the response body. The current implementation yields the full response as a single token
/// to keep the adapter buildable without azure-openai SDK. Full streaming is a Phase 10.x
/// enhancement once Azure.AI.OpenAI 2.x is stable on net10.
/// </para>
/// </summary>
internal sealed class OpenAiLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLlmClient> _logger;

    public OpenAiLlmClient(
        HttpClient http,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiLlmClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        string model = request.Model ?? _options.Model;

        var payload = BuildPayload(request, model, stream: false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload),
        };

        using HttpResponseMessage httpResponse = await _http
            .SendAsync(httpRequest, ct)
            .ConfigureAwait(false);

        httpResponse.EnsureSuccessStatusCode();

        using System.IO.Stream responseStream = await httpResponse.Content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);

        OpenAiChatCompletion? completion = await JsonSerializer
            .DeserializeAsync<OpenAiChatCompletion>(responseStream, cancellationToken: ct)
            .ConfigureAwait(false);

        if (completion is null)
        {
            throw new InvalidOperationException("Empty response from OpenAI API.");
        }

        string content = completion.Choices?.Count > 0
            ? completion.Choices[0].Message?.Content ?? string.Empty
            : string.Empty;

        return new ChatResponse
        {
            Message = new ChatMessage { Role = ChatRole.Assistant, Content = content },
            FinishReason = completion.Choices?.Count > 0
                ? completion.Choices[0].FinishReason ?? "stop"
                : "stop",
            Usage = new UsageInfo
            {
                PromptTokens = completion.Usage?.PromptTokens ?? 0,
                CompletionTokens = completion.Usage?.CompletionTokens ?? 0,
                TotalTokens = completion.Usage?.TotalTokens ?? 0,
            },
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // GAP: True SSE streaming parses "data: " prefixed lines from a chunked response.
        // For now we call the non-streaming endpoint and yield the response as a single token.
        // Replace with native streaming once Azure.AI.OpenAI 2.x ships net10 support.
        ChatResponse response = await ChatAsync(request, ct).ConfigureAwait(false);
        yield return response.Message.Content;
    }

    private static object BuildPayload(ChatRequest request, string model, bool stream)
    {
        var messages = new List<object>();
        foreach (ChatMessage msg in request.Messages)
        {
            messages.Add(new { role = msg.Role.ToString().ToLowerInvariant(), content = msg.Content });
        }

        return new
        {
            model,
            messages,
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            stream,
        };
    }

    // Minimal response shape for deserialization — avoids Newtonsoft.Json.
    private sealed class OpenAiChatCompletion
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public UsageData? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public MessageData? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class MessageData
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class UsageData
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}

/// <summary>Configuration options for the OpenAI provider.</summary>
public sealed class OpenAiOptions
{
    /// <summary>Gets or sets the OpenAI API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base endpoint URL.
    /// Defaults to "https://api.openai.com/v1/" for OpenAI; override for Azure OpenAI.
    /// </summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1/";

    /// <summary>Gets or sets the default chat model to use when the request does not specify one.</summary>
    public string Model { get; set; } = "gpt-4o-mini";
}
