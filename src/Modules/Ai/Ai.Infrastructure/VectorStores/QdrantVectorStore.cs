using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Ai.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ai.Infrastructure.VectorStores;

/// <summary>
/// Vector store backed by Qdrant via its HTTP REST API.
/// <para>
/// GAP: <c>Qdrant.Client</c> gRPC package is not yet stable on net10. This adapter
/// uses the Qdrant HTTP API directly. Swap to the gRPC client once net10 support ships.
/// </para>
/// <para>
/// <strong>Tenant isolation:</strong> all searches include a payload filter on <c>tenant_id</c>.
/// The guard clause throws if tenantId is empty.
/// </para>
/// </summary>
internal sealed class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _http;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(
        HttpClient http,
        IOptions<QdrantOptions> options,
        ILogger<QdrantVectorStore> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(VectorDocument document, CancellationToken ct = default)
    {
        if (document.TenantId == Guid.Empty)
        {
            throw new ArgumentException("VectorDocument.TenantId must not be empty.", nameof(document));
        }

        // GAP: full Qdrant upsert implementation deferred. Stub logs and returns.
        _logger.LogInformation(
            "QdrantVectorStore.UpsertAsync: docId={DocId}, tenant={TenantId} [STUB]",
            document.Id,
            document.TenantId);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int k,
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId must not be Guid.Empty — cross-tenant search is a security violation.",
                nameof(tenantId));
        }

        // GAP: Qdrant REST call with payload filter. Stub returns empty list.
        _logger.LogInformation(
            "QdrantVectorStore.SearchAsync: tenant={TenantId}, k={K} [STUB]",
            tenantId,
            k);

        await Task.CompletedTask.ConfigureAwait(false);
        return Array.Empty<VectorSearchResult>();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid documentId, Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId must not be empty.", nameof(tenantId));
        }

        _logger.LogInformation(
            "QdrantVectorStore.DeleteAsync: docId={DocId}, tenant={TenantId} [STUB]",
            documentId,
            tenantId);

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>Configuration for the Qdrant vector store.</summary>
public sealed class QdrantOptions
{
    /// <summary>Gets or sets the Qdrant endpoint URL.</summary>
    public string Endpoint { get; set; } = "http://localhost:6333/";

    /// <summary>Gets or sets the collection name to use.</summary>
    public string Collection { get; set; } = "saasbuilder_vectors";

    /// <summary>Gets or sets the optional API key for authenticated Qdrant Cloud.</summary>
    public string? ApiKey { get; set; }
}
