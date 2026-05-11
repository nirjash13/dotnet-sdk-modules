using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Contracts;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Application.Handlers;

/// <summary>
/// Handles the get-tenant-inspector admin query.
/// </summary>
public sealed class GetTenantInspectorHandler(ITenantDirectoryService directory)
{
    /// <summary>Returns full tenant inspector DTO or a not-found failure.</summary>
    public async Task<Result<TenantInspectorDto>> HandleAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        TenantInspectorDto? dto = await directory.GetTenantInspectorAsync(tenantId, ct).ConfigureAwait(false);

        if (dto is null)
        {
            return Result<TenantInspectorDto>.Failure($"Tenant '{tenantId}' not found.");
        }

        return Result<TenantInspectorDto>.Success(dto);
    }
}
