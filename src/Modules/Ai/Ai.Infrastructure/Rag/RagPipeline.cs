using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.Rag;

/// <summary>
/// Concrete RAG pipeline: embed → vector search (tenant-filtered) → prompt-stuffing → LLM chat.
/// <para>
/// <strong>Security invariant:</strong> the vector-store call includes a mandatory tenant filter.
/// If <paramref name="tenantId"/> is <see cref="Guid.Empty"/> this method throws
/// <see cref="ArgumentException"/> before any I/O occurs.  This guard is the last line of
/// defence at the application orchestration layer; <see cref="IVectorStore"/> implementations
/// enforce the same invariant independently.
/// </para>
/// </summary>
public sealed class RagPipeline : IRagPipeline
{
    private readonly IEmbeddingClient _embedding;
    private readonly IVectorStore _vectorStore;
    private readonly ILlmClient _llm;
    private readonly ILogger<RagPipeline> _logger;

    private const int DefaultK = 5;

    /// <summary>Initializes a new instance of <see cref="RagPipeline"/>.</summary>
    public RagPipeline(
        IEmbeddingClient embedding,
        IVectorStore vectorStore,
        ILlmClient llm,
        ILogger<RagPipeline> logger)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
        _llm = llm;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> QueryAsync(
        string question,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Guard clause — security invariant. Do not remove.
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId must not be Guid.Empty. Executing a RAG query without a tenant scope " +
                "would expose documents from all tenants and constitutes a cross-tenant data breach.",
                nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be empty.", nameof(question));
        }

        _logger.LogInformation(
            "RagPipeline.QueryAsync: tenantId={TenantId}, question length={Len}",
            tenantId,
            question.Length);

        // Step 1: embed the question.
        float[] queryEmbedding = await _embedding
            .EmbedAsync(question, ct)
            .ConfigureAwait(false);

        // Step 2: vector search — tenant_id filter is MANDATORY and enforced here AND in IVectorStore.
        IReadOnlyList<VectorSearchResult> chunks = await _vectorStore
            .SearchAsync(queryEmbedding, DefaultK, tenantId, ct)
            .ConfigureAwait(false);

        // Step 3: prompt-stuffing — build context from retrieved chunks.
        string context = chunks.Count == 0
            ? "No relevant documents found."
            : string.Join("\n\n---\n\n", chunks.Select((c, i) => $"[{i + 1}] {c.Content}"));

        string systemPrompt =
            "You are a helpful assistant. Answer the user's question using ONLY the context " +
            "provided below. If the answer is not in the context, say you don't know.\n\n" +
            $"CONTEXT:\n{context}";

        // Step 4: LLM chat.
        var chatRequest = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatRole.System, Content = systemPrompt },
                new ChatMessage { Role = ChatRole.User, Content = question },
            },
        };

        ChatResponse response = await _llm
            .ChatAsync(chatRequest, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RagPipeline.QueryAsync completed: tenantId={TenantId}, chunks={ChunkCount}, tokens={Tokens}",
            tenantId,
            chunks.Count,
            response.Usage.TotalTokens);

        return response;
    }
}
