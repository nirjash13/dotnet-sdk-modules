using System.Threading;
using System.Threading.Tasks;

namespace Search.Application.Abstractions;

/// <summary>
/// Subscribes to domain events and keeps the search index up-to-date.
/// Default implementation is <c>EfCoreSearchIndexer</c> in Search.Infrastructure.
/// TODO(Phase 5.6): implement cross-tenant indexing prevention test (TenantA docs not visible to TenantB search).
/// </summary>
public interface ISearchIndexer
{
    /// <summary>
    /// Re-indexes the entity with the given <paramref name="entityId"/> in the <paramref name="index"/>.
    /// </summary>
    Task IndexEntityAsync(string index, string entityId, CancellationToken ct = default);

    /// <summary>Removes a document from the index.</summary>
    Task RemoveEntityAsync(string index, string entityId, CancellationToken ct = default);
}
