using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaasBuilder.SharedKernel.Tenancy;
using Search.Infrastructure.Options;
using AppSearchQuery = Search.Application.Models.SearchQuery;
using ISearchClientContract = Search.Application.Abstractions.ISearchClient;
using MeiliIndex = Meilisearch.Index;

namespace Search.Infrastructure.Adapters;

/// <summary>
/// Meilisearch search client.
/// Tenant scoping is enforced by including a filter on <c>tenantId</c> in every query.
/// Documents are stored with a top-level <c>tenantId</c> field.
/// </summary>
internal sealed class MeilisearchAdapter(
    IOptions<MeilisearchOptions> options,
    ITenantContextAccessor tenantAccessor,
    ILogger<MeilisearchAdapter> logger)
    : ISearchClientContract
{
    private readonly MeilisearchOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task IndexAsync<T>(string index, T document, CancellationToken ct = default)
        where T : class
    {
        Guid tenantId = RequireTenantId();
        MeilisearchClient client = new MeilisearchClient(_opts.Url, _opts.ApiKey);
        MeiliIndex idx = client.Index(index);

        // Inject tenantId into the document dictionary.
        string json = JsonSerializer.Serialize(document);
        Dictionary<string, object?> dict =
            JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
            ?? new Dictionary<string, object?>();
        dict["tenantId"] = tenantId.ToString();

        TaskInfo result = await idx
            .AddDocumentsAsync(new[] { dict }, primaryKey: "id", cancellationToken: ct)
            .ConfigureAwait(false);

        logger.LogDebug(
            "Search.Meilisearch: indexed document in '{Index}' (tenant={TenantId}, taskUid={Uid})",
            index, tenantId, result.TaskUid);
    }

    /// <inheritdoc />
    public async Task<Search.Application.Models.SearchResults<T>> SearchAsync<T>(
        AppSearchQuery query,
        CancellationToken ct = default)
        where T : class
    {
        Guid tenantId = RequireTenantId();

        // Enforce caller's tenant — overwrite any cross-tenant scope attempt.
        query.TenantScope = tenantId.ToString();

        MeilisearchClient client = new MeilisearchClient(_opts.Url, _opts.ApiKey);
        MeiliIndex idx = client.Index(query.Index);

        SearchQuery meiliQuery = new SearchQuery
        {
            Filter = (object)$"tenantId = \"{tenantId}\"",
            Offset = (query.Page - 1) * query.PageSize,
            Limit = query.PageSize,
        };

        ISearchable<Dictionary<string, object?>> rawResults =
            await idx.SearchAsync<Dictionary<string, object?>>(
                query.Text ?? string.Empty,
                meiliQuery,
                ct)
            .ConfigureAwait(false);

        // SearchResult<T> implements ISearchable<T> and carries EstimatedTotalHits.
        SearchResult<Dictionary<string, object?>>? results =
            rawResults as SearchResult<Dictionary<string, object?>>;

        IReadOnlyCollection<Dictionary<string, object?>> hits = rawResults.Hits;

        T[] items = hits
            .Select(hit =>
            {
                Dictionary<string, object?> clean = new Dictionary<string, object?>(hit);
                clean.Remove("tenantId");
                string docJson = JsonSerializer.Serialize(clean);
                return JsonSerializer.Deserialize<T>(docJson);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        int total = results is not null ? results.EstimatedTotalHits : items.Length;

        return new Search.Application.Models.SearchResults<T>
        {
            Total = total,
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
    {
        MeilisearchClient client = new MeilisearchClient(_opts.Url, _opts.ApiKey);
        MeiliIndex idx = client.Index(index);
        await idx.DeleteOneDocumentAsync(documentId, ct).ConfigureAwait(false);
    }

    private Guid RequireTenantId()
    {
        Guid? id = tenantAccessor.Current?.TenantId;
        return id ?? throw new InvalidOperationException("No tenant context for Meilisearch operation.");
    }
}
