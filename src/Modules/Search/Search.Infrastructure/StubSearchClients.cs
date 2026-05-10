using System.Threading;
using System.Threading.Tasks;
using Search.Application.Abstractions;
using Search.Application.Models;

namespace Search.Infrastructure;

// ---------------------------------------------------------------------------
// Cloud search provider stubs — Phase 5.6
// ---------------------------------------------------------------------------

/// <summary>TODO(Phase 5.6): OpenSearch adapter — install OpenSearch.Client NuGet package.</summary>
internal sealed class OpenSearchClient : ISearchClient
{
    public Task IndexAsync<T>(string index, T document, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): OpenSearch integration");

    public Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): OpenSearch integration");

    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.6): OpenSearch integration");
}

/// <summary>TODO(Phase 5.6): Meilisearch adapter — install Meilisearch.Net NuGet package.</summary>
internal sealed class MeilisearchClient : ISearchClient
{
    public Task IndexAsync<T>(string index, T document, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): Meilisearch integration");

    public Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): Meilisearch integration");

    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.6): Meilisearch integration");
}

/// <summary>TODO(Phase 5.6): Typesense adapter — install Typesense NuGet package.</summary>
internal sealed class TypesenseClient : ISearchClient
{
    public Task IndexAsync<T>(string index, T document, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): Typesense integration");

    public Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): Typesense integration");

    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.6): Typesense integration");
}

/// <summary>TODO(Phase 5.6): Algolia adapter — install Algolia.Search NuGet package (check commercial license terms).</summary>
internal sealed class AlgoliaClient : ISearchClient
{
    public Task IndexAsync<T>(string index, T document, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): Algolia integration");

    public Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.6): Algolia integration");

    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.6): Algolia integration");
}
