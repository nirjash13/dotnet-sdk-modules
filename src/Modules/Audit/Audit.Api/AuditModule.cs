using System;
using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Audit.Api;

/// <summary><see cref="IModuleStartup"/> for the Audit module.</summary>
public sealed class AuditModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddAuditInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/audit")
            .WithTags("audit")
            .RequireAuthorization("AdminPolicy");

        // GET /api/v1/audit/events?from=...&to=...&actor=...&resource=...
        group.MapGet("/events", GetEventsAsync)
            .WithName("Audit_GetEvents")
            .WithSummary("Returns paginated audit events. Admin-only.");
    }

    private static async Task<IResult> GetEventsAsync(
        IAuditEventQuery query,
        ITenantContextAccessor tenantAccessor,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? actor = null,
        string? resource = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        (int total, System.Collections.Generic.IReadOnlyList<Audit.Contracts.AuditEventDto> items) =
            await query.QueryAsync(ctx.TenantId, from, to, actor, resource, page, pageSize, ct)
                .ConfigureAwait(false);

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }
}
