using System;
using System.Threading;
using System.Threading.Tasks;
using Ai.Contracts;

namespace Ai.Application.Abstractions;

/// <summary>
/// Retrieval-Augmented Generation pipeline that answers a natural-language question
/// using tenant-scoped documents from the vector store.
/// <para>
/// Pipeline: embed question → vector search (tenant-filtered) → prompt-stuffing → LLM chat.
/// </para>
/// </summary>
public interface IRagPipeline
{
    /// <summary>
    /// Answers <paramref name="question"/> using documents retrieved exclusively from the
    /// <paramref name="tenantId"/> partition of the vector store.
    /// </summary>
    /// <param name="question">The user's question in natural language.</param>
    /// <param name="tenantId">
    /// The tenant whose document corpus to search.
    /// Must not be <see cref="Guid.Empty"/> — passing an empty GUID is a programming error
    /// and will throw <see cref="ArgumentException"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The model's answer grounded in the tenant's documents.</returns>
    Task<ChatResponse> QueryAsync(string question, Guid tenantId, CancellationToken ct = default);
}
