using System.Collections.Generic;

namespace Search.Application.Models;

/// <summary>
/// Parameters for a full-text search request.
/// <see cref="TenantScope"/> is automatically populated by infrastructure from
/// <c>ITenantContextAccessor.Current.TenantId</c> — callers must NOT set it manually.
/// </summary>
public sealed class SearchQuery
{
    /// <summary>Gets or sets the logical index name (e.g. "products").</summary>
    public string Index { get; set; } = string.Empty;

    /// <summary>Gets or sets the free-text search term.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets field-level filter expressions.</summary>
    public IReadOnlyDictionary<string, string>? Filters { get; set; }

    /// <summary>Gets or sets fields to facet on.</summary>
    public IReadOnlyList<string>? Facets { get; set; }

    /// <summary>Gets or sets the sort field and direction (e.g. "price:asc").</summary>
    public string? Sort { get; set; }

    /// <summary>Gets or sets fields to highlight in results.</summary>
    public IReadOnlyList<string>? Highlight { get; set; }

    /// <summary>Gets or sets the 1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Gets or sets the page size (max 100).</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the tenant scope guard. Set automatically by infrastructure.
    /// Invariant: search NEVER returns documents from other tenants.
    /// </summary>
    public string? TenantScope { get; set; }
}
