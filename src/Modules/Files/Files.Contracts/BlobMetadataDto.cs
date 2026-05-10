using System;

namespace Files.Contracts;

/// <summary>Metadata about a blob returned from the Files API.</summary>
/// <param name="Key">Unique blob key within the tenant's storage scope.</param>
/// <param name="ContentType">MIME content type.</param>
/// <param name="SizeBytes">Size in bytes.</param>
/// <param name="CreatedAt">Upload timestamp (UTC).</param>
public record BlobMetadataDto(
    string Key,
    string? ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);
