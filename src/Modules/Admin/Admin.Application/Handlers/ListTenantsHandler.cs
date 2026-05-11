using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Contracts;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Application.Handlers;

/// <summary>
/// Handles the list-tenants admin query.
/// </summary>
public sealed class ListTenantsHandler(ITenantDirectoryService directory)
{
    /// <summary>
    /// Executes the query and returns a paginated list of tenant summaries.
    /// </summary>
    public async Task<Result<(int Total, IReadOnlyList<TenantSummaryDto> Items)>> HandleAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)
        {
            return Result<(int, IReadOnlyList<TenantSummaryDto>)>.Failure("page must be >= 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return Result<(int, IReadOnlyList<TenantSummaryDto>)>.Failure("pageSize must be 1-100.");
        }

        (int total, IReadOnlyList<TenantSummaryDto> items) =
            await directory.ListTenantsAsync(search, status, page, pageSize, ct).ConfigureAwait(false);

        return Result<(int, IReadOnlyList<TenantSummaryDto>)>.Success((total, items));
    }
}
