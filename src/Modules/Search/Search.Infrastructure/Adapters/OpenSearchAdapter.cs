using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;
using SaasBuilder.SharedKernel.Tenancy;
using Search.Infrastructure.Options;
using AppSearchQuery = Search.Application.Models.SearchQuery;
using ISearchClientContract = Search.Application.Abstractions.ISearchClient;

namespace Search.Infrastructure.Adapters;

/// <summary>
/// OpenSearch search client.
/// Tenant-scope is enforced by injecting a term filter on <c>tenantId</c> into every query.
/// All documents are stored with a top-level <c>tenantId</c> field.
/// Index naming: <c>{prefix}_{indexName}</c> (shared per-deployment, tenant-filtered at query time).
/// </summary>
internal sealed class OpenSearchAdapter(
    IOptions<OpenSearchOptions> options,
    ITenantContextAccessor tenantAccessor,
    ILogger<OpenSearchAdapter> logger)
    : ISearchClientContract
{
    private readonly OpenSearchOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task IndexAsync<T>(string index, T document, CancellationToken ct = default)
        where T : class
    {
        Guid tenantId = RequireTenantId();
        OpenSearchClient client = BuildClient();
        string indexName = IndexName(index);

        // Serialize document and re-hydrate as dictionary to inject tenantId.
        string json = JsonSerializer.Serialize(document);
        Dictionary<string, object?>? dict =
            JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        if (dict is null)
        {
            logger.LogWarning("Search.OpenSearch: failed to serialize document for indexing.");
            return;
        }

        dict["tenantId"] = tenantId.ToString();

        IndexResponse response = await client.IndexAsync(
            dict,
            r => r.Index(indexName),
            ct).ConfigureAwait(false);

        if (!response.IsValid)
        {
            logger.LogWarning(
                "Search.OpenSearch: indexing failed for index '{Index}' (tenant={TenantId}): {Error}",
                indexName, tenantId, response.DebugInformation);
        }
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

        OpenSearchClient client = BuildClient();
        string indexName = IndexName(query.Index);
        string tenantFilter = tenantId.ToString();
        string? queryText = query.Text;
        int from = (query.Page - 1) * query.PageSize;
        int size = query.PageSize;

        ISearchResponse<Dictionary<string, object?>> response =
            await client.SearchAsync<Dictionary<string, object?>>(
                s => s
                    .Index(indexName)
                    .From(from)
                    .Size(size)
                    .Query(
                        q =>
                        {
                            QueryContainer tenantTerm = q.Term("tenantId", tenantFilter);
                            if (!string.IsNullOrWhiteSpace(queryText))
                            {
                                return tenantTerm && q.QueryString(qs => qs.Query(queryText));
                            }

                            return tenantTerm;
                        }),
                ct)
            .ConfigureAwait(false);

        if (!response.IsValid)
        {
            logger.LogWarning(
                "Search.OpenSearch: query failed for index '{Index}': {Error}",
                indexName, response.DebugInformation);
            return new Search.Application.Models.SearchResults<T> { Total = 0, Items = Array.Empty<T>() };
        }

        T[] items = response.Documents
            .Select(hit =>
            {
                // Remove tenantId before projecting back.
                Dictionary<string, object?> clean = new Dictionary<string, object?>(hit);
                clean.Remove("tenantId");
                string hitJson = JsonSerializer.Serialize(clean);
                return JsonSerializer.Deserialize<T>(hitJson);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        return new Search.Application.Models.SearchResults<T>
        {
            Total = (int)response.Total,
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
    {
        OpenSearchClient client = BuildClient();
        string indexName = IndexName(index);

        DeleteResponse response = await client.DeleteAsync(
            new DeleteRequest(indexName, documentId),
            ct).ConfigureAwait(false);

        if (!response.IsValid && response.Result != Result.NotFound)
        {
            logger.LogWarning(
                "Search.OpenSearch: delete failed for document '{Id}' in index '{Index}': {Error}",
                documentId,
                indexName,
                response.DebugInformation);
        }
    }

    private OpenSearchClient BuildClient()
    {
        if (!string.IsNullOrWhiteSpace(_opts.Username) && !string.IsNullOrWhiteSpace(_opts.Password))
        {
            ConnectionSettings settings = new ConnectionSettings(
                new SingleNodeConnectionPool(new Uri(_opts.Uri)))
                .BasicAuthentication(_opts.Username, _opts.Password);
            return new OpenSearchClient(settings);
        }

        return new OpenSearchClient(new Uri(_opts.Uri));
    }

    private string IndexName(string index) => $"{_opts.IndexPrefix}_{index}".ToLowerInvariant();

    private Guid RequireTenantId()
    {
        Guid? id = tenantAccessor.Current?.TenantId;
        return id ?? throw new InvalidOperationException("No tenant context for OpenSearch operation.");
    }
}
