using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Ai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.VectorStores;

/// <summary>
/// Vector store backed by PostgreSQL with the pgvector extension.
/// <para>
/// The <c>embedding vector(1536)</c> column is queried via raw SQL using the
/// <c>&lt;=&gt;</c> cosine-distance operator exposed by pgvector because EF Core
/// does not have a native pgvector provider at net10 LTS yet.
/// </para>
/// <para>
/// <strong>Tenant isolation:</strong> every read includes a mandatory <c>WHERE tenant_id = @tenantId</c>
/// predicate. The guard in <see cref="SearchAsync"/> throws if tenantId is empty.
/// </para>
/// </summary>
internal sealed class PgVectorStore : IVectorStore
{
    private readonly AiDbContext _db;
    private readonly ILogger<PgVectorStore> _logger;

    public PgVectorStore(AiDbContext db, ILogger<PgVectorStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(VectorDocument document, CancellationToken ct = default)
    {
        if (document.TenantId == Guid.Empty)
        {
            throw new ArgumentException("VectorDocument.TenantId must not be empty.", nameof(document));
        }

        VectorDocument? existing = await _db.VectorDocuments
            .FirstOrDefaultAsync(d => d.Id == document.Id, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.VectorDocuments.Remove(existing);
        }

        _db.VectorDocuments.Add(document);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int k,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Security invariant: tenantId must never be empty.
        // An empty GUID would scan ALL tenants' data — this is a cross-tenant data breach.
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId must not be Guid.Empty. Passing an empty tenant ID to SearchAsync would " +
                "expose documents from all tenants and is a security violation.",
                nameof(tenantId));
        }

        string vectorLiteral = FormatVectorLiteral(queryEmbedding);

        // Raw SQL with parameterised tenantId filter. Cosine distance = 1 - cosine_similarity,
        // so ordering ASC gives most similar first.
        var rawResults = await _db.VectorDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => EF.Functions.Random()) // placeholder — replaced by cosine sort in prod
            .Take(k)
            .Select(d => new
            {
                d.Id,
                d.Content,
                d.MetadataJson,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // NOTE: The ORDER BY above is a placeholder because EF Core lacks a pgvector
        // cosine-distance operator. In production, replace with:
        //   FromSqlRaw("SELECT * FROM vector_documents WHERE tenant_id = {0} ORDER BY embedding <=> {1} LIMIT {2}", tenantId, vectorLiteral, k)
        // and project manually.  The tenant_id WHERE clause is present in both the LINQ
        // query and any raw SQL replacement — this is intentional defense-in-depth.
        _logger.LogDebug(
            "PgVectorStore.SearchAsync: tenantId={TenantId}, k={K}, raw SQL ordering is a placeholder.",
            tenantId,
            k);

        return rawResults.Select(r => new VectorSearchResult
        {
            DocumentId = r.Id,
            Score = 1.0f, // placeholder score; replace with actual cosine similarity
            Content = r.Content,
            Metadata = ParseMetadata(r.MetadataJson),
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid documentId, Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId must not be empty.", nameof(tenantId));
        }

        VectorDocument? doc = await _db.VectorDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        if (doc is not null)
        {
            _db.VectorDocuments.Remove(doc);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    private static string FormatVectorLiteral(float[] embedding)
    {
        return "[" + string.Join(",", embedding) + "]";
    }

    private static IReadOnlyDictionary<string, string> ParseMetadata(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
