using System;
using SaasBuilder.SharedKernel.Tenancy;

namespace Search.Infrastructure.Entities;

/// <summary>
/// Generic search document stored in the <c>search_documents</c> table.
/// Each row contains the original JSON content plus a pre-computed <c>tsvector</c> for FTS.
/// </summary>
public sealed class SearchDocument : ITenantScoped
{
    private SearchDocument()
    {
        IndexName = string.Empty;
        DocumentId = string.Empty;
        ContentJson = string.Empty;
    }

    /// <summary>Initializes a new search document.</summary>
    public SearchDocument(
        Guid id,
        string indexName,
        Guid tenantId,
        string documentId,
        string contentJson,
        DateTimeOffset indexedAt)
    {
        Id = id;
        IndexName = indexName;
        TenantId = tenantId;
        DocumentId = documentId;
        ContentJson = contentJson;
        IndexedAt = indexedAt;
    }

    /// <summary>Gets the row identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the logical index name.</summary>
    public string IndexName { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the application-level document identifier.</summary>
    public string DocumentId { get; private set; }

    /// <summary>Gets the serialized document as JSON.</summary>
    public string ContentJson { get; private set; }

    /// <summary>Gets the pre-computed tsvector (managed by Postgres raw SQL in upsert).</summary>
    public string? SearchVector { get; private set; }

    /// <summary>Gets when the document was last indexed.</summary>
    public DateTimeOffset IndexedAt { get; private set; }
}
