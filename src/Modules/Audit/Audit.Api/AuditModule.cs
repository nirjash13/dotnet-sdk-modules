using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Contracts;
using Audit.Infrastructure.Extensions;
using Audit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Abstractions;

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
        AuditDbContext db,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? actor = null,
        string? resource = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        IQueryable<Audit.Infrastructure.Entities.AuditEntry> query = db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp);

        if (from.HasValue)
        {
            query = query.Where(e => e.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.Timestamp <= to.Value);
        }

        if (!string.IsNullOrEmpty(actor))
        {
            query = query.Where(e => e.ActorId == actor);
        }

        if (!string.IsNullOrEmpty(resource))
        {
            query = query.Where(e => e.ResourceType == resource);
        }

        int total = await query.CountAsync(ct).ConfigureAwait(false);

        System.Collections.Generic.List<AuditEventDto> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditEventDto(
                e.Id,
                e.TenantId,
                e.ActorId,
                e.Action,
                e.ResourceType,
                e.ResourceId,
                e.IpAddress,
                e.CorrelationId,
                e.Timestamp))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }
}
