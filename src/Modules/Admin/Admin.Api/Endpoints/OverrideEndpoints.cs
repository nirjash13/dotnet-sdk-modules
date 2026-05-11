using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Handlers;
using Admin.Application.Validators;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Api.Endpoints;

/// <summary>
/// Admin override endpoints for entitlements and feature flags.
/// </summary>
internal static class OverrideEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/tenants/{id:guid}/entitlements/{key}", OverrideEntitlementAsync)
            .WithName("Admin_OverrideEntitlement")
            .WithSummary("Writes a tenant-level entitlement override (bypasses edition grants).");

        group.MapPut("/tenants/{id:guid}/feature-flags/{key}", OverrideFeatureFlagAsync)
            .WithName("Admin_OverrideFeatureFlag")
            .WithSummary("Writes a tenant-level feature-flag override.");
    }

    private static async Task<IResult> OverrideEntitlementAsync(
        Guid id,
        string key,
        OverrideEntitlementRequest request,
        [FromServices] IValidator<OverrideEntitlementRequest> validator,
        [FromServices] OverrideEntitlementHandler handler,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        Result result = await handler.HandleAsync(
            id, key, request.Value!, request.Reason!, ct).ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "tenant.entitlement_override",
            targetTenantId: id,
            payloadJson: $"{{\"key\":\"{key}\",\"value\":\"{request.Value}\"}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> OverrideFeatureFlagAsync(
        Guid id,
        string key,
        OverrideFeatureFlagRequest request,
        [FromServices] IValidator<OverrideFeatureFlagRequest> validator,
        [FromServices] OverrideFeatureFlagHandler handler,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        Result result = await handler.HandleAsync(
            id, key, request.Value!.Value, request.Reason!, ct).ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "tenant.feature_flag_override",
            targetTenantId: id,
            payloadJson: $"{{\"key\":\"{key}\",\"value\":{request.Value!.Value.ToString().ToLowerInvariant()}}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }
}
