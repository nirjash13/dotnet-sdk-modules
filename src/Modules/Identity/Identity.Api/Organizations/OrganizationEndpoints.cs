using System;
using System.Security.Claims;
using FluentValidation;
using Identity.Application.Organizations;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Organizations;

/// <summary>
/// Minimal API endpoints for Organization management (Phase 2 scaffold).
/// All endpoints are authenticated by default; invitation acceptance is AllowAnonymous
/// because the invitee authenticates via the token itself.
/// </summary>
public static class OrganizationEndpoints
{
    /// <summary>Maps all organization and invitation endpoints onto the given route group.</summary>
    public static void MapOrganizationEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder orgs = endpoints
            .MapGroup("/api/v1/organizations")
            .RequireAuthorization()
            .WithTags("organizations");

        orgs.MapPost(string.Empty, CreateOrganization)
            .WithName("Organizations_Create")
            .WithSummary("Create a new organization within the current tenant.");

        orgs.MapGet(string.Empty, ListOrganizations)
            .WithName("Organizations_List")
            .WithSummary("List organizations for the current tenant.");

        orgs.MapGet("{id:guid}", GetOrganization)
            .WithName("Organizations_GetById")
            .WithSummary("Get a single organization by id.");

        orgs.MapPatch("{id:guid}", RenameOrganization)
            .WithName("Organizations_Rename")
            .WithSummary("Rename an organization.");

        orgs.MapPost("{id:guid}/members:invite", InviteMember)
            .WithName("Organizations_InviteMember")
            .WithSummary("Invite a user to the organization by email.");

        orgs.MapPatch("{id:guid}/members/{memberId:guid}/role", ChangeMemberRole)
            .WithName("Organizations_ChangeMemberRole")
            .WithSummary("Change a member's role.");

        orgs.MapDelete("{id:guid}/members/{memberId:guid}", RemoveMember)
            .WithName("Organizations_RemoveMember")
            .WithSummary("Remove a member from the organization.");

        orgs.MapPost("{id:guid}:transfer-ownership", TransferOwnership)
            .WithName("Organizations_TransferOwnership")
            .WithSummary("Initiate an ownership transfer (confirmation email sent).");

        // Invitation acceptance is token-bearing — no JWT required.
        endpoints
            .MapPost("/api/v1/invitations/{token}:accept", AcceptInvitation)
            .AllowAnonymous()
            .WithTags("invitations")
            .WithName("Invitations_Accept")
            .WithSummary("Accept a pending invitation using the raw token.");
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        ClaimsPrincipal user,
        CreateOrganizationHandler handler,
        IValidator<CreateOrganizationCommand> validator,
        HttpContext context)
    {
        Guid tenantId = GetTenantId(user);
        Guid userId = GetUserId(user);

        // TODO(Phase 2 — implementation): resolve OwnerRoleId from config/registry.
        Guid ownerRoleId = GetOwnerRoleIdFromQuery(context);

        CreateOrganizationCommand command = new CreateOrganizationCommand(
            TenantId: tenantId,
            Slug: request.Slug,
            Name: request.Name,
            OwnerUserId: userId,
            OwnerRoleId: ownerRoleId);

        // Validate at the API boundary — throws ValidationException mapped to 400 by ProblemDetailsExceptionHandler.
        await validator.ValidateAndThrowAsync(command, context.RequestAborted).ConfigureAwait(false);

        SaasBuilder.SharedKernel.Abstractions.Result<CreateOrganizationResult> result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status409Conflict,
                title: "Organization creation failed");
        }

        return Results.Created(
            $"/api/v1/organizations/{result.Value!.OrganizationId}",
            new CreateOrganizationResponse(result.Value.OrganizationId));
    }

    private static async Task<IResult> ListOrganizations(
        ClaimsPrincipal user,
        IdentityDbContext dbContext,
        HttpContext context)
    {
        Guid tenantId = GetTenantId(user);

        // Project to DTO — never return EF entities.
        System.Collections.Generic.List<OrganizationSummaryResponse> orgs = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationSummaryResponse(
                o.Id,
                o.Slug,
                o.Name,
                o.Status.ToString()))
            .ToListAsync(context.RequestAborted);

        return Results.Ok(orgs);
    }

    private static async Task<IResult> GetOrganization(
        Guid id,
        ClaimsPrincipal user,
        IdentityDbContext dbContext,
        HttpContext context)
    {
        Guid tenantId = GetTenantId(user);

        OrganizationSummaryResponse? org = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Id == id && o.TenantId == tenantId)
            .Select(o => new OrganizationSummaryResponse(
                o.Id,
                o.Slug,
                o.Name,
                o.Status.ToString()))
            .FirstOrDefaultAsync(context.RequestAborted);

        return org is null ? Results.NotFound() : Results.Ok(org);
    }

    private static async Task<IResult> RenameOrganization(
        Guid id,
        [FromBody] RenameOrganizationRequest request,
        IOrganizationRepository repository,
        HttpContext context)
    {
        Identity.Domain.Organizations.Organization? org =
            await repository.FindByIdAsync(id, context.RequestAborted);

        if (org is null)
        {
            return Results.NotFound();
        }

        org.Rename(request.Name);
        await repository.SaveChangesAsync(context.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> InviteMember(
        Guid id,
        [FromBody] InviteMemberRequest request,
        ClaimsPrincipal user,
        InviteMemberHandler handler,
        HttpContext context)
    {
        Guid userId = GetUserId(user);

        InviteMemberCommand command = new InviteMemberCommand(
            OrganizationId: id,
            Email: request.Email,
            RoleId: request.RoleId,
            InvitedByUserId: userId);

        SaasBuilder.SharedKernel.Abstractions.Result<InviteMemberResult> result =
            await handler.HandleAsync(command, context.RequestAborted);

        return result.IsSuccess
            ? Results.Ok(new InviteMemberResponse(result.Value!.InvitationId))
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> AcceptInvitation(
        string token,
        [FromBody] AcceptInvitationRequest request,
        AcceptInvitationHandler handler,
        HttpContext context)
    {
        AcceptInvitationCommand command = new AcceptInvitationCommand(
            RawToken: token,
            UserId: request.UserId);

        SaasBuilder.SharedKernel.Abstractions.Result<AcceptInvitationResult> result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invitation acceptance failed");
        }

        return Results.Ok(new AcceptInvitationResponse(
            result.Value!.OrganizationId,
            result.Value.MemberId));
    }

    private static async Task<IResult> ChangeMemberRole(
        Guid id,
        Guid memberId,
        [FromBody] ChangeMemberRoleRequest request,
        ClaimsPrincipal user,
        ChangeMemberRoleHandler handler,
        HttpContext context)
    {
        ChangeMemberRoleCommand command = new ChangeMemberRoleCommand(
            OrganizationId: id,
            MemberId: memberId,
            NewRoleId: request.NewRoleId,
            OwnerRoleId: request.OwnerRoleId,
            RequestingUserId: GetUserId(user));

        SaasBuilder.SharedKernel.Abstractions.Result result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            // Last-owner-protection returns 422 Unprocessable Entity.
            return result.Error!.Contains("Owner", StringComparison.OrdinalIgnoreCase)
                ? Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
                : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveMember(
        Guid id,
        Guid memberId,
        [FromBody] RemoveMemberRequest request,
        ClaimsPrincipal user,
        RemoveMemberHandler handler,
        HttpContext context)
    {
        RemoveMemberCommand command = new RemoveMemberCommand(
            OrganizationId: id,
            MemberId: memberId,
            OwnerRoleId: request.OwnerRoleId,
            RequestingUserId: GetUserId(user));

        SaasBuilder.SharedKernel.Abstractions.Result result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return result.Error!.Contains("Owner", StringComparison.OrdinalIgnoreCase)
                ? Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
                : Results.Problem(result.Error, statusCode: StatusCodes.Status404NotFound);
        }

        return Results.NoContent();
    }

    private static IResult TransferOwnership(
        Guid id,
        [FromBody] TransferOwnershipRequest request,
        ClaimsPrincipal user)
    {
        // TODO(Phase 2 — implementation): wire TransferOwnershipHandler once notification
        // infrastructure (Phase 5) is available. Returning 501 until then.
        return Results.Problem(
            detail: "Ownership transfer is not yet implemented. See TODO(Phase 2 — implementation) in TransferOwnershipHandler.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    // ── Claim helpers ─────────────────────────────────────────────────────────

    private static Guid GetTenantId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirstValue("tenant_id");
        return raw is not null && Guid.TryParse(raw, out Guid id) ? id : Guid.Empty;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return raw is not null && Guid.TryParse(raw, out Guid id) ? id : Guid.Empty;
    }

    private static Guid GetOwnerRoleIdFromQuery(HttpContext context)
    {
        // TODO(Phase 2 — implementation): resolve OwnerRoleId from IPermissionRegistry
        // or a well-known seed constant rather than a query parameter.
        // For the scaffold, accept it from a header or use a well-known placeholder.
        string? raw = context.Request.Headers["X-Owner-Role-Id"];
        return raw is not null && Guid.TryParse(raw, out Guid id) ? id : Guid.Empty;
    }
}
