using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marketplace.Application.Abstractions;
using Marketplace.Contracts;
using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.SharedKernel.Abstractions;

namespace Marketplace.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IMarketplaceAppRegistry"/>.</summary>
internal sealed class EfMarketplaceAppRegistry : IMarketplaceAppRegistry
{
    private readonly MarketplaceDbContext _db;

    public EfMarketplaceAppRegistry(MarketplaceDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MarketplaceAppDto>> ListAppsAsync(CancellationToken ct = default)
    {
        return await _db.Apps
            .AsNoTracking()
            .Where(a => a.IsListed)
            .OrderBy(a => a.Name)
            .Select(a => new MarketplaceAppDto
            {
                Id = a.Id,
                Slug = a.Slug,
                Name = a.Name,
                Vendor = a.Vendor,
                Description = a.Description,
                IconUrl = a.IconUrl,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<MarketplaceAppDto>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        MarketplaceAppDto? dto = await _db.Apps
            .AsNoTracking()
            .Where(a => a.Slug == slug)
            .Select(a => new MarketplaceAppDto
            {
                Id = a.Id,
                Slug = a.Slug,
                Name = a.Name,
                Vendor = a.Vendor,
                Description = a.Description,
                IconUrl = a.IconUrl,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return dto is null
            ? Result<MarketplaceAppDto>.Failure($"App '{slug}' not found.")
            : Result<MarketplaceAppDto>.Success(dto);
    }
}
