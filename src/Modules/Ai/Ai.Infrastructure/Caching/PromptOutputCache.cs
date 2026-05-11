using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.Caching;

/// <summary>
/// Decorator around <see cref="ILlmClient"/> that caches non-streaming chat responses
/// using <see cref="IMemoryCache"/> keyed on SHA-256(prompt+model+temperature).
/// Cache TTL is 1 hour. Bypass with request header <c>X-Ai-Cache-Bypass: true</c>.
/// </summary>
public sealed class PromptOutputCache : ILlmClient
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly ILlmClient _inner;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PromptOutputCache> _logger;

    /// <summary>Initializes a new instance of <see cref="PromptOutputCache"/>.</summary>
    public PromptOutputCache(
        ILlmClient inner,
        IMemoryCache cache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PromptOutputCache> logger)
    {
        _inner = inner;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        // Check bypass header.
        if (IsCacheBypassRequested())
        {
            _logger.LogDebug("PromptOutputCache: bypass header present, skipping cache.");
            return await _inner.ChatAsync(request, ct).ConfigureAwait(false);
        }

        string cacheKey = ComputeCacheKey(request);

        if (_cache.TryGetValue(cacheKey, out ChatResponse? cached) && cached is not null)
        {
            _logger.LogDebug("PromptOutputCache: cache hit for key {Key}.", cacheKey[..8]);
            return cached;
        }

        ChatResponse response = await _inner.ChatAsync(request, ct).ConfigureAwait(false);

        _cache.Set(cacheKey, response, CacheTtl);
        _logger.LogDebug("PromptOutputCache: cached response for key {Key}.", cacheKey[..8]);

        return response;
    }

    /// <inheritdoc/>
    public System.Collections.Generic.IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        CancellationToken ct = default)
    {
        // Streaming responses are not cached — pass through to the inner client.
        return _inner.StreamChatAsync(request, ct);
    }

    private bool IsCacheBypassRequested()
    {
        HttpContext? ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            return false;
        }

        return ctx.Request.Headers.TryGetValue("X-Ai-Cache-Bypass", out var values) &&
               string.Equals(values.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeCacheKey(ChatRequest request)
    {
        // Key = SHA-256 of (serialised messages) + model + temperature
        var sb = new StringBuilder();
        foreach (ChatMessage msg in request.Messages)
        {
            sb.Append(msg.Role).Append(':').Append(msg.Content).Append('|');
        }

        sb.Append(request.Model ?? string.Empty);
        sb.Append(':').Append(request.Temperature?.ToString("F4") ?? "null");

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }
}
