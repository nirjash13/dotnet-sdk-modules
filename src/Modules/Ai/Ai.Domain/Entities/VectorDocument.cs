using System;
using SaasBuilder.SharedKernel.Tenancy;

namespace Ai.Domain.Entities;

/// <summary>
/// A text chunk stored in the vector store, scoped to a single tenant.
/// The embedding is serialised as JSON because the domain layer has no
/// dependency on pgvector or any other vector representation.
/// </summary>
public sealed class VectorDocument : ITenantScoped
{
    /// <summary>Gets the unique document identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the plain-text content of this chunk.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the embedding serialised as a JSON float array, e.g. "[0.1,0.2,…]".
    /// Stored separately from the pgvector column so the domain entity remains
    /// infrastructure-agnostic.
    /// </summary>
    public string EmbeddingJson { get; private set; } = "[]";

    /// <summary>Gets free-form JSON metadata (source URL, page number, etc.).</summary>
    public string MetadataJson { get; private set; } = "{}";

    /// <summary>Gets the UTC timestamp when the document was indexed.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core parameterless constructor.
    private VectorDocument() { }

    /// <summary>Creates a new vector document for the given tenant.</summary>
    public static VectorDocument Create(
        Guid tenantId,
        string content,
        string embeddingJson,
        string metadataJson = "{}")
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content must not be empty.", nameof(content));
        }

        return new VectorDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Content = content,
            EmbeddingJson = string.IsNullOrWhiteSpace(embeddingJson) ? "[]" : embeddingJson,
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
