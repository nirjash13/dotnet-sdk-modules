using System.Threading;
using System.Threading.Tasks;
using Search.Application.Models;

namespace Search.Application.Abstractions;

/// <summary>
/// Tenant-scoped full-text search client.
/// All operations automatically include a tenant scope guard — implementations
/// MUST ensure results never leak documents from other tenants.
/// </summary>
public interface ISearchClient
{
    /// <summary>Indexes or re-indexes a document.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="index">Logical index name (e.g. "products", "invoices").</param>
    /// <param name="document">The document to index.</param>
    /// <param name="ct">Cancellation token.</param>
    Task IndexAsync<T>(string index, T document, CancellationToken ct = default)
        where T : class;

    /// <summary>Executes a search query and returns results scoped to the current tenant.</summary>
    Task<SearchResults<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct = default)
        where T : class;

    /// <summary>Removes a document from the index.</summary>
    Task DeleteAsync(string index, string documentId, CancellationToken ct = default);
}
