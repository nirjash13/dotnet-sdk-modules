using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Application.Abstractions;
using Gdpr.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Gdpr.Infrastructure.Data;

/// <summary>EF Core implementation of <see cref="IGdprSubProcessorRepository"/>.</summary>
internal sealed class GdprSubProcessorRepository : IGdprSubProcessorRepository
{
    private readonly GdprDbContext _db;

    public GdprSubProcessorRepository(GdprDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubProcessorDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.SubProcessors
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubProcessorDto(s.Id, s.Name, s.Country, s.Purpose, s.DataTypes, s.Website, s.AddedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SubProcessorDto> CreateAsync(
        string name,
        string country,
        string purpose,
        string dataTypes,
        string website,
        CancellationToken ct = default)
    {
        var entity = new GdprSubProcessor
        {
            Id = Guid.NewGuid(),
            Name = name,
            Country = country,
            Purpose = purpose,
            DataTypes = dataTypes,
            Website = website,
            AddedAt = DateTimeOffset.UtcNow,
        };

        _db.SubProcessors.Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new SubProcessorDto(entity.Id, entity.Name, entity.Country, entity.Purpose, entity.DataTypes, entity.Website, entity.AddedAt);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        GdprSubProcessor? entity = await _db.SubProcessors
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        _db.SubProcessors.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
