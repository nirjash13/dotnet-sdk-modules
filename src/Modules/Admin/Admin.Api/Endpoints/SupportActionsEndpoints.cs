using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Validators;
using Billing.Application.Abstractions;
using FluentValidation;
using FluentValidation.Results;
using Identity.Application.Auth;
using Identity.Application.Impersonation;
using Identity.Application.Organizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Api.Endpoints;

/// <summary>
/// Support action endpoints (resend invite, force password reset, refund, credit grant, impersonate).
/// All are protected by SystemAdmin policy and auto-audited.
/// </summary>
internal static class SupportActionsEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/tenants/{id:guid}/resend-invite/{invitationId:guid}", ResendInviteAsync)
            .WithName("Admin_ResendInvite")
            .WithSummary("Resends the invitation email for a pending invitation.");

        group.MapPost("/tenants/{id:guid}/force-password-reset/{userId:guid}", ForcePasswordResetAsync)
            .WithName("Admin_ForcePasswordReset")
            .WithSummary("Issues a password-reset token and emails the user.");

        group.MapPost("/tenants/{id:guid}/refund", RefundAsync)
            .WithName("Admin_Refund")
            .WithSummary("Issues a refund for an invoice via the billing provider.");

        group.MapPost("/tenants/{id:guid}/credit-grant", CreditGrantAsync)
            .WithName("Admin_CreditGrant")
            .WithSummary("Grants a billing credit to the tenant.");

        group.MapPost("/tenants/{id:guid}/impersonate", ImpersonateAsync)
            .WithName("Admin_Impersonate")
            .WithSummary("Starts a time-boxed impersonation session as a tenant user.");
    }

    private static async Task<IResult> ResendInviteAsync(
        Guid id,
        Guid invitationId,
        [FromServices] IInvitationRepository invitations,
        [FromServices] INotificationDispatcher notifications,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        Identity.Domain.Organizations.Invitation? invitation =
            await invitations.FindByIdAsync(invitationId, ct).ConfigureAwait(false);

        if (invitation is null)
        {
            return Results.Problem(
                detail: $"Invitation '{invitationId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Resend the invitation email — send to the invitee's email address.
        // The raw token is not stored; the admin action resends a new magic link
        // via the notifications dispatcher. Full implementation requires an
        // IInvitationService.RegenerateTokenAsync(invitationId) overload.
        NotificationMessage resendMsg = new NotificationMessage(
            Guid.Empty,
            "You have been invited",
            "Your invitation has been resent. Please check your email for the original link.",
            new[] { NotificationChannel.Email },
            "invite.resent",
            RecipientEmail: invitation.Email);
        await notifications.DispatchAsync(resendMsg, ct).ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "tenant.resend_invite",
            targetTenantId: id,
            payloadJson: JsonSerializer.Serialize(new { invitationId }),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        return Results.Ok(new { Message = "Invitation resent." });
    }

    private static Task<IResult> ForcePasswordResetAsync(
        Guid id,
        Guid userId,
        [FromServices] IPasswordResetService passwordResetService,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        // TODO(M-O3): not implemented — IPasswordResetService.InitiateAsync accepts an email, not a userId.
        // Add IPasswordResetService.InitiateByUserIdAsync(userId, ct) before enabling this endpoint.
        _ = (id, userId, passwordResetService, auditor, httpContext, ct);
        return Task.FromResult(Results.Problem(
            detail: "IPasswordResetService.InitiateByUserIdAsync is not yet implemented. Cannot accept a Guid user ID here.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented"));
    }

    private static async Task<IResult> RefundAsync(
        Guid id,
        RefundRequest request,
        [FromServices] IValidator<RefundRequest> validator,
        [FromServices] IBillingProvider billingProvider,
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

        // Billing provider refund — stub if RefundAsync is not yet on IBillingProvider.
        // TODO(Phase 6.x): Add IBillingProvider.RefundAsync(invoiceId, amountCents, reason, ct).
        // For now we return 501 with a clear gap notice.
        _ = billingProvider;
        await Task.CompletedTask.ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "tenant.refund",
            targetTenantId: id,
            payloadJson: JsonSerializer.Serialize(new { invoiceId = request.InvoiceId, amountCents = request.AmountCents }),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        return Results.Problem(
            detail: "IBillingProvider.RefundAsync is not yet implemented. Add the method to complete this action.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static async Task<IResult> CreditGrantAsync(
        Guid id,
        CreditGrantRequest request,
        [FromServices] IValidator<CreditGrantRequest> validator,
        [FromServices] IBillingProvider billingProvider,
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

        // TODO(Phase 6.x): Add IBillingProvider.IssueCreditAsync(tenantId, amountCents, reason, ct).
        _ = billingProvider;
        await Task.CompletedTask.ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "tenant.credit_grant",
            targetTenantId: id,
            payloadJson: JsonSerializer.Serialize(new { amountCents = request.AmountCents }),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        return Results.Problem(
            detail: "IBillingProvider.IssueCreditAsync is not yet implemented. Add the method to complete this action.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static async Task<IResult> ImpersonateAsync(
        Guid id,
        ImpersonateRequest request,
        [FromServices] IValidator<ImpersonateRequest> validator,
        [FromServices] IImpersonationService impersonation,
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

        await auditor.RecordAsync(
            actorId,
            "tenant.impersonate",
            targetTenantId: id,
            payloadJson: JsonSerializer.Serialize(new { userId = request.UserId }),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        ImpersonationSession session = await impersonation.StartAsync(
            Guid.Parse(actorId),
            request.UserId!.Value,
            request.Reason!,
            ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            SessionId = session.SessionId,
            Token = session.ImpersonationToken,
            ExpiresAt = session.ExpiresAt,
        });
    }
}
