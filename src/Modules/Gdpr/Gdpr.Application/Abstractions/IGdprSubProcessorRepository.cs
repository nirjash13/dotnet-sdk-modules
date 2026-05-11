using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Contracts;

namespace Gdpr.Application.Abstractions;

/// <summary>Repository for sub-processor records.</summary>
public interface IGdprSubProcessorRepository
{
    Task<IReadOnlyList<SubProcessorDto>> GetAllAsync(CancellationToken ct = default);

    Task<SubProcessorDto> CreateAsync(
        string name,
        string country,
        string purpose,
        string dataTypes,
        string website,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
