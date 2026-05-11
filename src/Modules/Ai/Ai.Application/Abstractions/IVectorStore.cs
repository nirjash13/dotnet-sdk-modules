using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ai.Contracts;
using Ai.Domain.Entities;

namespace Ai.Application.Abstractions;

/// <summary>
/// Abstraction for a tenant-isolated vector store.
/// <para>
/// <strong>Security invariant:</strong> every call that reads data MUST filter by
/// <paramref name="tenantId"/>. Implementations that skip this filter will cause
/// cross-tenant data leakage.  See <c>RagPipeline</c> for the guard clause that
/// enforces this invariant at the orchestration layer.
/// </para>
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Upserts a document into the vector store.
    /// The document's <see cref="VectorDocument.TenantId"/> provides the isolation boundary.
    /// </summary>
    /// <param name="document">The document to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertAsync(VectorDocument document, CancellationToken ct = default);

    /// <summary>
    /// Finds the <paramref name="k"/> nearest neighbours to the query embedding,
    /// restricted exclusively to documents belonging to <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="queryEmbedding">The query vector to compare against.</param>
    /// <param name="k">Maximum number of results to return.</param>
    /// <param name="tenantId">
    /// The tenant scope. Must not be <see cref="Guid.Empty"/>; the implementation
    /// must enforce this as a hard filter, not a hint.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of matching documents, most similar first.</returns>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int k,
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>Deletes a document by identifier. Scoped to the given tenant.</summary>
    Task DeleteAsync(Guid documentId, Guid tenantId, CancellationToken ct = default);
}
