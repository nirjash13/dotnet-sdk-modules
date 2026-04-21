using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Chassis.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Chassis.Host.Tenancy;

/// <summary>
/// ASP.NET Core middleware that resolves the tenant identity from the authenticated principal
/// or a service-account header and populates <see cref="ITenantContextAccessor.Current"/>.
/// </summary>
/// <remarks>
/// Resolution order:
/// <list type="number">
///   <item>JWT <c>tenant_id</c> claim — populated by the auth scheme after Phase 2 wire-up.</item>
///   <item><c>X-Tenant-Id</c> header — for service accounts and dev/test without a real JWT.</item>
/// </list>
/// Infrastructure endpoints (<c>/health</c>, <c>/openapi</c>, <c>/scalar</c>) bypass tenant
/// enforcement — they do not serve tenant-scoped data.
/// Returns HTTP 401 with ProblemDetails when neither source provides a parseable tenant GUID.
/// </remarks>
internal sealed class TenantMiddleware
{
    // Paths that are allowed without a tenant context.
    // Health checks and OpenAPI docs are infrastructure endpoints, not tenant-scoped resources.
    // OIDC endpoints (/connect/*) are the token issuance surface — they establish identity
    // before a tenant context exists, so they must be bypassed here.
    // Well-known discovery /.well-known/* is also pre-authentication.
    private static readonly string[] _bypassPaths =
    [
        "/health",
        "/openapi",
        "/scalar",
        "/connect",
        "/.well-known",
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantContextAccessor)
    {
        // Skip tenant enforcement for infrastructure endpoints.
        // Match exact path OR path that begins with "<prefix>/" to prevent a bypass of
        // "/connect" from accidentally matching "/connectmore" or similar path prefixes.
        string requestPath = context.Request.Path.Value ?? string.Empty;
        foreach (string bypass in _bypassPaths)
        {
            bool isExact = string.Equals(requestPath, bypass, StringComparison.OrdinalIgnoreCase);
            bool isPrefix = requestPath.StartsWith(bypass + "/", StringComparison.OrdinalIgnoreCase);
            if (isExact || isPrefix)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }
        }

        Guid? tenantId = ResolveTenantId(context);

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

    private static Guid? ResolveTenantId(HttpContext context)
    {
        // 1. JWT claim (Phase 2 will populate this via JwtBearer; in Phase 1 the anonymous
        //    scheme does not issue claims, so this path evaluates as absent).
        string? claimValue = context.User?.FindFirstValue(TenantClaims.TenantId);
        if (Guid.TryParse(claimValue, out Guid fromClaim))
        {
            return fromClaim;
        }

        // 2. X-Tenant-Id header — accepted ONLY when no JWT principal is authenticated
        //    OR when the authenticated principal carries a "service-account" role.
        //
        //    Rationale: end-user JWTs always carry the tenant_id claim (path 1 above).
        //    Allowing the header unconditionally would let an attacker holding any valid
        //    JWT substitute a different tenant by injecting the header.
        //    Service accounts (client_credentials flow) may not carry tenant_id in the JWT
        //    depending on enrichment timing, so the header is the correct channel for them.
        bool principalIsAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        bool principalIsServiceAccount = context.User?.IsInRole("service-account") == true;

        if (!principalIsAuthenticated || principalIsServiceAccount)
        {
            string? headerValue = context.Request.Headers["X-Tenant-Id"].ToString();
            if (Guid.TryParse(headerValue, out Guid fromHeader))
            {
                return fromHeader;
            }
        }

        return null;
    }

    private static Guid? ResolveUserId(HttpContext context)
    {
        string? claimValue = context.User?.FindFirstValue(TenantClaims.UserId)
                          ?? context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out Guid id) ? id : null;
    }
}
