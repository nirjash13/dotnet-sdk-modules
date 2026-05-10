using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaasBuilder.Host.Configuration.Options;
using SaasBuilder.SharedKernel.Tenancy;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Tenancy;

/// <summary>
/// ASP.NET Core middleware that resolves the tenant identity by running the configured
/// <see cref="ITenantResolver"/> pipeline and populating <see cref="ITenantContextAccessor.Current"/>.
/// </summary>
/// <remarks>
/// The pipeline evaluates resolvers in descending priority order; the first non-null result wins.
/// Paths listed in <see cref="SaasBuilderTenancyOptions.AnonymousBypass"/> skip tenant enforcement.
/// Returns HTTP 401 with ProblemDetails when no resolver provides a tenant identity.
/// </remarks>
internal sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;
    private readonly IReadOnlySet<string> _anonymousBypass;

    public TenantMiddleware(
        RequestDelegate next,
        ILogger<TenantMiddleware> logger,
        IOptions<TenantMiddlewareOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _anonymousBypass = options?.Value.AnonymousBypass
            ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextAccessor tenantContextAccessor,
        IEnumerable<ITenantResolver> resolvers)
    {
        string requestPath = context.Request.Path.Value ?? string.Empty;

        // Skip tenant enforcement for configured anonymous-bypass paths.
        foreach (string bypass in _anonymousBypass)
        {
            bool isExact = string.Equals(requestPath, bypass, StringComparison.OrdinalIgnoreCase);
            bool isPrefix = requestPath.StartsWith(bypass + "/", StringComparison.OrdinalIgnoreCase);
            if (isExact || isPrefix)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }
        }

        CancellationToken ct = context.RequestAborted;

        // Run resolvers in descending priority order; first non-null wins.
        IOrderedEnumerable<ITenantResolver> orderedResolvers = resolvers
            .OrderByDescending(r => r.Priority);

        Guid? tenantId = null;
        foreach (ITenantResolver resolver in orderedResolvers)
        {
            Guid? resolved = await resolver.ResolveAsync(context, ct).ConfigureAwait(false);
            if (resolved.HasValue)
            {
                tenantId = resolved;
                break;
            }
        }

        if (tenantId is null)
        {
            _logger.LogWarning(
                "Request to {Path} rejected: no resolvable tenant identity.",
                context.Request.Path);
            throw new MissingTenantException();
        }

        Guid? userId = ResolveUserId(context);
        string? correlationId = context.Request.Headers["correlation-id"].ToString()
            is { Length: > 0 } cid ? cid : null;

        tenantContextAccessor.Current = new TenantContext(tenantId.Value, userId, correlationId);

        _logger.LogDebug(
            "Tenant context established: TenantId={TenantId}, UserId={UserId}",
            tenantId,
            userId);

        await _next(context).ConfigureAwait(false);
    }

    private static Guid? ResolveUserId(HttpContext context)
    {
        string? claimValue = context.User?.FindFirstValue(TenantClaims.UserId)
                          ?? context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out Guid id) ? id : null;
    }
}
