using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Search.Application.Abstractions;
using Search.Application.Models;
using Search.Infrastructure.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Search.Infrastructure;

/// <summary>
/// Postgres full-text search client using EF Core raw SQL queries with <c>tsvector</c>.
/// Tenant scope is enforced via a <c>WHERE tenant_id = @currentTenant</c> clause
/// as defence-in-depth on top of RLS policies.
/// </summary>
internal sealed class PostgresFullTextSearchClient(
    SearchDbContext db,
    ITenantContextAccessor tenantAccessor,
    ILogger<PostgresFullTextSearchClient> logger)
    : ISearchClient
{
    /// <inheritdoc />
    public async Task IndexAsync<T>(string index, T document, CancellationToken ct = default)
        where T : class
    {
        Guid tenantId = RequireTenantId();

        string json = JsonSerializer.Serialize(document);
        string? documentId = GetDocumentId(document);

        if (documentId is null)
        {
            logger.LogWarning("Search.Postgres: document type {Type} has no 'Id' property. Skipping index.", typeof(T).Name);
            return;
        }

        // Upsert into the generic search_documents table.
        // TODO(Phase 5.6): use a typed table per index for better performance.
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO search.search_documents (id, index_name, tenant_id, document_id, content_json, search_vector, indexed_at)
            VALUES (gen_random_uuid(), {0}, {1}, {2}, {3}::jsonb, to_tsvector('english', {3}), now())
            ON CONFLICT (index_name, tenant_id, document_id)
            DO UPDATE SET content_json = EXCLUDED.content_json, search_vector = EXCLUDED.search_vector, indexed_at = now()
            """,
            new object[] { index, tenantId, documentId, json },
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default)
        where T : class
    {
        Guid tenantId = RequireTenantId();

        // Enforce tenant scope — defence-in-depth in addition to RLS.
        query.TenantScope = tenantId.ToString();

        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return new SearchResults<T> { Total = 0, Items = Array.Empty<T>() };
        }

        int offset = (query.Page - 1) * query.PageSize;

        List<string> jsonResults = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT content_json::text
                FROM search.search_documents
                WHERE index_name = {0}
                  AND tenant_id = {1}
                  AND search_vector @@ plainto_tsquery('english', {2})
                ORDER BY ts_rank(search_vector, plainto_tsquery('english', {2})) DESC
                LIMIT {3} OFFSET {4}
                """,
                query.Index,
                tenantId,
                query.Text,
                query.PageSize,
                offset)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<T> items = new List<T>(jsonResults.Count);
        foreach (string json in jsonResults)
        {
            T? obj = JsonSerializer.Deserialize<T>(json);
            if (obj is not null)
            {
                items.Add(obj);
            }
        }

        return new SearchResults<T> { Total = items.Count, Items = items };
    }

    /// <inheritdoc />
    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
    {
        Guid tenantId = RequireTenantId();

        return db.Database.ExecuteSqlRawAsync(
            "DELETE FROM search.search_documents WHERE index_name = {0} AND tenant_id = {1} AND document_id = {2}",
            new object[] { index, tenantId, documentId },
            ct);
    }

    private Guid RequireTenantId()
    {
        Guid? id = tenantAccessor.Current?.TenantId;
        return id ?? throw new InvalidOperationException("No tenant context for search operation.");
    }

    private static string? GetDocumentId(object document)
    {
        System.Reflection.PropertyInfo? idProp = document.GetType()
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        return idProp?.GetValue(document)?.ToString();
    }
}
