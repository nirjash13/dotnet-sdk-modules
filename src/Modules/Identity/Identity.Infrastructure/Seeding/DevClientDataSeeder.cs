using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Tenancy;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Hosted service that seeds the development OAuth client <c>chassis-dev</c> when
/// <c>Identity:SeedDevClient=true</c> in configuration.
/// </summary>
/// <remarks>
/// The seed is idempotent — checks whether the client already exists before inserting.
/// Use in Development environments only. Guarded by an environment check at startup.
/// TODO (Phase 3): replace EnsureCreatedAsync with MigrateAsync once EF migrations are scaffolded.
/// </remarks>
internal sealed class DevClientDataSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevClientDataSeeder> logger)
    : IHostedService
{
    // The fixed dev-tenant GUID used for client_credentials tokens in Development/Testing.
    // Matches the Phase 1 test convention (tenant A = ...0001).
    // Declared before private const to satisfy SA1202 ('internal' before 'private').
    internal static readonly Guid DevTenantId = new Guid("00000000-0000-0000-0000-000000000001");

    private const string DevClientId = "chassis-dev";

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        bool seedEnabled = configuration.GetValue<bool>("Identity:SeedDevClient");
        if (!seedEnabled)
        {
            logger.LogDebug("DevClientDataSeeder: seeding disabled (Identity:SeedDevClient is false).");
            return;
        }

        // Guard: seeder must never run outside Development/Testing to prevent accidental
        // client creation in Staging or Production environments.
        if (!environment.IsDevelopment() && !string.Equals(
                environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DevClientDataSeeder must not run outside Development or Testing environments. " +
                "Set Identity:SeedDevClient=false in non-development configuration.");
        }

        // Read the client secret from configuration — never hardcode secrets.
        string clientSecret = configuration["Identity:DevClient:Secret"]
            ?? throw new InvalidOperationException(
                "Identity:DevClient:Secret must be set in configuration when Identity:SeedDevClient=true. " +
                "Add it to appsettings.Development.json or the test configuration.");

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        // Open a bypass scope so the interceptor and global query filter do not require
        // a tenant context during schema creation and client seeding (Blocker #3).
        ITenantContextAccessor accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using IDisposable bypass = accessor.BeginBypass();

        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // TODO (Phase 3): replace with db.Database.MigrateAsync(cancellationToken) once
        // EF migrations are scaffolded for the Identity module.
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        IOpenIddictApplicationManager manager =
            scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        object? existing = await manager
            .FindByClientIdAsync(DevClientId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            logger.LogInformation("DevClientDataSeeder: client '{ClientId}' already exists — skipping.", DevClientId);
            return;
        }

        OpenIddictApplicationDescriptor descriptor = BuildDescriptor(clientSecret);
        await manager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("DevClientDataSeeder: client '{ClientId}' seeded.", DevClientId);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static OpenIddictApplicationDescriptor BuildDescriptor(string clientSecret)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = DevClientId,
            ClientSecret = clientSecret,
            ClientType = ClientTypes.Confidential,
            DisplayName = "Chassis Dev Client",
        };

        // Store the dev-tenant ID in application properties so TenantClaimEnricher can
        // emit a tenant_id claim for client_credentials tokens from this client (Major #10).
        // OpenIddict stores Properties as ImmutableDictionary<string, JsonElement>, so the
        // value must be serialized as a JsonElement (a JSON string literal).
        descriptor.Properties["tenant_id"] = System.Text.Json.JsonSerializer.SerializeToElement(DevTenantId.ToString());

        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(Permissions.Scopes.Email);
        descriptor.Permissions.Add(Permissions.Scopes.Profile);
        descriptor.Permissions.Add(Permissions.Scopes.Roles);
        descriptor.Permissions.Add($"{Permissions.Prefixes.Scope}tenant");

        descriptor.RedirectUris.Add(new Uri("https://localhost:7001/signin-oidc"));
        descriptor.RedirectUris.Add(new Uri("https://oauth.pstmn.io/v1/callback"));
        descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:7001/signout-callback-oidc"));

        return descriptor;
    }
}
