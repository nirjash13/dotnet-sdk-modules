using System.Collections.Generic;

namespace Search.Application.Models;

/// <summary>Search results returned by <see cref="Abstractions.ISearchClient"/>.</summary>
/// <typeparam name="T">The document type.</typeparam>
public sealed class SearchResults<T>
    where T : class
{
    /// <summary>Gets or sets the total number of matching documents (before pagination).</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the current page of results.</summary>
    public IReadOnlyList<T> Items { get; set; } = System.Array.Empty<T>();

    /// <summary>Gets or sets the facet counts keyed by field name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>? Facets { get; set; }
}
