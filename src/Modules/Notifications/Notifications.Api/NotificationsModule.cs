using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Infrastructure.Extensions;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Notifications.Api;

/// <summary>
/// <see cref="IModuleStartup"/> for the Notifications module.
/// </summary>
public sealed class NotificationsModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddNotificationsInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/notifications")
            .WithTags("notifications")
            .RequireAuthorization();

        // GET /api/v1/notifications/inbox
        group.MapGet("/inbox", GetInboxAsync)
            .WithName("Notifications_GetInbox")
            .WithSummary("Returns the authenticated user's in-app notification inbox.");

        // POST /api/v1/notifications/{id}:read
        group.MapPost("/{id:guid}:read", MarkReadAsync)
            .WithName("Notifications_MarkRead")
            .WithSummary("Marks a notification as read.");

        // GET /api/v1/notifications/preferences
        group.MapGet("/preferences", GetPreferencesAsync)
            .WithName("Notifications_GetPreferences")
            .WithSummary("Returns the user's notification channel preferences.");

        // PUT /api/v1/notifications/preferences/{key}
        group.MapPut("/preferences/{key}", UpdatePreferenceAsync)
            .WithName("Notifications_UpdatePreference")
            .WithSummary("Enables or disables a notification preference.");
    }

    private static async Task<IResult> GetInboxAsync(
        IInAppNotificationStore store,
        ITenantContextAccessor tenantAccessor,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null || ctx.UserId is null)
        {
            return Results.Unauthorized();
        }

        System.Collections.Generic.IReadOnlyList<Notifications.Domain.Entities.InAppNotification> items =
            await store.GetInboxAsync(ctx.TenantId, ctx.UserId.Value, page, pageSize, ct)
                .ConfigureAwait(false);

        System.Collections.Generic.IReadOnlyList<InAppNotificationDto> dtos =
            System.Linq.Enumerable.Select(items, n => new InAppNotificationDto(
                n.Id, n.UserId, n.Title, n.Body, n.ActionUrl, n.ReadAt, n.CreatedAt))
            .ToList();

        return Results.Ok(new { Page = page, PageSize = pageSize, Items = dtos });
    }

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        IInAppNotificationStore store,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        Notifications.Domain.Entities.InAppNotification? notification =
            await store.FindAsync(id, ct).ConfigureAwait(false);

        if (notification is null || notification.TenantId != ctx.TenantId)
        {
            return Results.NotFound();
        }

        notification.MarkRead(DateTimeOffset.UtcNow);
        await store.SaveChangesAsync(ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static Task<IResult> GetPreferencesAsync(
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        // TODO(Phase 5.1): return preferences from NotificationPreference entity store.
        // For Phase 5 scaffold, return an empty list.
        _ = tenantAccessor;
        _ = ct;
        return Task.FromResult(Results.Ok(System.Array.Empty<object>()));
    }

    private static Task<IResult> UpdatePreferenceAsync(
        string key,
        UpdatePreferenceRequest request,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        // TODO(Phase 5.1): persist preference via NotificationPreference entity store.
        _ = key;
        _ = request;
        _ = tenantAccessor;
        _ = ct;
        return Task.FromResult<IResult>(Results.Accepted());
    }

    /// <summary>Request body for updating a notification preference.</summary>
    public sealed class UpdatePreferenceRequest
    {
        /// <summary>Gets or sets a value indicating whether this preference is enabled.</summary>
        public bool Enabled { get; set; }
    }
}
