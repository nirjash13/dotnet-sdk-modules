using System;

namespace Gdpr.Contracts;

/// <summary>A GDPR sub-processor entry.</summary>
public sealed record SubProcessorDto(
    Guid Id,
    string Name,
    string Country,
    string Purpose,
    string DataTypes,
    string Website,
    DateTimeOffset AddedAt);
