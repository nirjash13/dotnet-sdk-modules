using System;
using System.Collections.Generic;

namespace Ai.Contracts;

/// <summary>A single document returned by a vector similarity search.</summary>
public sealed class VectorSearchResult
{
    /// <summary>Gets or sets the unique identifier of the matched document.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Gets or sets the cosine similarity score in the range [0, 1].</summary>
    public float Score { get; set; }

    /// <summary>Gets or sets the document's stored metadata key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; set; }
        = new Dictionary<string, string>();

    /// <summary>Gets or sets the raw text content of the document chunk.</summary>
    public string Content { get; set; } = string.Empty;
}
