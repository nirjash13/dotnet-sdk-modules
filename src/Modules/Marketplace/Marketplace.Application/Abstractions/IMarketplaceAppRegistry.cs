using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marketplace.Contracts;
using SaasBuilder.SharedKernel.Abstractions;

namespace Marketplace.Application.Abstractions;

/// <summary>Read-side registry for marketplace app listings.</summary>
public interface IMarketplaceAppRegistry
{
    /// <summary>Returns all publicly listed apps in alphabetical order by name.</summary>
    Task<IReadOnlyList<MarketplaceAppDto>> ListAppsAsync(CancellationToken ct = default);

    /// <summary>Returns a single app by its slug, or a failure result if not found.</summary>
    Task<Result<MarketplaceAppDto>> GetBySlugAsync(string slug, CancellationToken ct = default);
}
