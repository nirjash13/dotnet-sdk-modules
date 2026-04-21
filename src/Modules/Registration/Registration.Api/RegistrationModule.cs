using System;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Registration.Contracts;
using Registration.Infrastructure.Extensions;

namespace Registration.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Registration module.
/// Discovered by <c>ReflectionModuleLoader</c> at startup via assembly scan.
/// </summary>
/// <remarks>
/// Exposes <c>POST /api/v1/registrations</c> which publishes <see cref="AssociationRegistrationStarted"/>
/// and returns 202 Accepted with the correlation id. The saga orchestrates the rest asynchronously.
/// </remarks>
public sealed class RegistrationModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddRegistrationInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/registrations", HandleStartRegistrationAsync)
            .AllowAnonymous(); // Registration is pre-authentication — the tenant does not exist yet.
    }

    private static async Task<IResult> HandleStartRegistrationAsync(
        StartRegistrationRequest request,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        var correlationId = Guid.NewGuid();

        await publishEndpoint.Publish(
            new AssociationRegistrationStarted
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                TenantId = request.TenantId != Guid.Empty ? request.TenantId : Guid.NewGuid(),
                AssociationName = request.AssociationName,
                PrimaryUserEmail = request.PrimaryUserEmail,
                Currency = request.Currency,
                StartedAt = DateTimeOffset.UtcNow,
            },
            ct).ConfigureAwait(false);

        return Results.Accepted(
            value: new { correlationId, message = "Registration started. Poll /api/v1/registrations/{correlationId} for status." });
    }

    /// <summary>Request body for POST /api/v1/registrations.</summary>
    private sealed class StartRegistrationRequest
    {
        /// <summary>Gets or sets the tenant id to provision (optional; generated if not supplied).</summary>
        public Guid TenantId { get; set; }

        /// <summary>Gets or sets the human-readable association name.</summary>
        public string AssociationName { get; set; } = string.Empty;

        /// <summary>Gets or sets the primary user's email address.</summary>
        public string PrimaryUserEmail { get; set; } = string.Empty;

        /// <summary>Gets or sets the ISO 4217 currency code for the ledger.</summary>
        public string Currency { get; set; } = "USD";
    }
}
