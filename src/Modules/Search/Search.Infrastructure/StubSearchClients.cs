// Stub search clients removed in Phase 5.6 — real implementations are in Search.Infrastructure.Adapters namespace.
// TypesenseClient, AlgoliaClient remain as future placeholders (not registered).
using System.Threading;
using System.Threading.Tasks;
using Search.Application.Abstractions;
using Search.Application.Models;

namespace Search.Infrastructure;

/// <summary>
/// TODO(Phase 5.x): Typesense adapter — install Typesense NuGet package.
/// TODO(M-O4): stub — register conditionally only when adapter implemented
///   (gate on <c>Search:Provider=Typesense</c> + <c>SaasBuilder:Search:Typesense:Enabled=true</c>).
/// </summary>
internal sealed class TypesenseClient : ISearchClient
{
    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task IndexAsync<T>(string index, T document, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.x): Typesense integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.x): Typesense integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.x): Typesense integration");
}

/// <summary>
/// TODO(Phase 5.x): Algolia adapter — install Algolia.Search NuGet package (check commercial license terms).
/// TODO(M-O4): stub — register conditionally only when adapter implemented
///   (gate on <c>Search:Provider=Algolia</c> + <c>SaasBuilder:Search:Algolia:Enabled=true</c>).
/// </summary>
internal sealed class AlgoliaClient : ISearchClient
{
    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task IndexAsync<T>(string index, T document, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.x): Algolia integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default) where T : class
        => throw new System.NotImplementedException("TODO(Phase 5.x): Algolia integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task DeleteAsync(string index, string documentId, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.x): Algolia integration");
}
