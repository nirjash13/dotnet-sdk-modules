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
/// LLM client adapter for Anthropic (api.anthropic.com/v1/messages).
/// Uses a thin <see cref="HttpClient"/> adapter with the <c>x-api-key</c> header.
/// <para>
/// GAP: The <c>Anthropic.SDK</c> NuGet package was not yet available for net10.0 at the
/// time of authoring. This adapter implements the essential subset of the Messages API.
/// Replace with the SDK once net10 compatibility is confirmed.
/// </para>
/// </summary>
internal sealed class AnthropicLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicLlmClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public AnthropicLlmClient(
        HttpClient http,
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicLlmClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        string model = request.Model ?? _options.Model;

        // Anthropic splits the system message from the messages array.
        string? systemPrompt = null;
        var messages = new List<object>();

        foreach (ChatMessage msg in request.Messages)
        {
            if (msg.Role == ChatRole.System)
            {
                systemPrompt = msg.Content;
            }
            else
            {
                messages.Add(new
                {
                    role = msg.Role == ChatRole.Assistant ? "assistant" : "user",
                    content = msg.Content,
                });
            }
        }

        var payload = new
        {
            model,
            max_tokens = request.MaxTokens ?? 1024,
            system = systemPrompt,
            messages,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "messages")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        using HttpResponseMessage httpResponse = await _http
            .SendAsync(httpRequest, ct)
            .ConfigureAwait(false);

        httpResponse.EnsureSuccessStatusCode();

        using System.IO.Stream stream = await httpResponse.Content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);

        AnthropicResponse? apiResponse = await JsonSerializer
            .DeserializeAsync<AnthropicResponse>(stream, JsonOptions, ct)
            .ConfigureAwait(false);

        if (apiResponse is null)
        {
            throw new InvalidOperationException("Empty response from Anthropic API.");
        }

        string content = apiResponse.Content?.Count > 0
            ? apiResponse.Content[0].Text ?? string.Empty
            : string.Empty;

        return new ChatResponse
        {
            Message = new ChatMessage { Role = ChatRole.Assistant, Content = content },
            FinishReason = apiResponse.StopReason ?? "stop",
            Usage = new UsageInfo
            {
                PromptTokens = apiResponse.Usage?.InputTokens ?? 0,
                CompletionTokens = apiResponse.Usage?.OutputTokens ?? 0,
                TotalTokens = (apiResponse.Usage?.InputTokens ?? 0) + (apiResponse.Usage?.OutputTokens ?? 0),
            },
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // GAP: Anthropic SSE streaming is not yet implemented. Fall back to non-streaming.
        ChatResponse response = await ChatAsync(request, ct).ConfigureAwait(false);
        yield return response.Message.Content;
    }

    // Minimal deserialization types for Anthropic Messages API.
    private sealed class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<ContentBlock>? Content { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private sealed class ContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}

/// <summary>Configuration options for the Anthropic provider.</summary>
public sealed class AnthropicOptions
{
    /// <summary>Gets or sets the Anthropic API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the default model (e.g. "claude-3-5-sonnet-20241022").</summary>
    public string Model { get; set; } = "claude-3-5-haiku-20241022";
}
