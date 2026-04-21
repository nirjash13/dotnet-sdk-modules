using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Host;
using Chassis.SharedKernel.Tenancy;
using FluentAssertions;
using Ledger.Domain.Entities;
using Ledger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Chassis.IntegrationTests.Phase3;

/// <summary>
/// Load-bearing IDOR-resistance integration test for the Ledger module.
///
/// Acceptance criterion (Phase 3):
/// POST /api/v1/ledger/accounts/{aId}/transactions with Tenant-A JWT → 201.
/// GET /api/v1/ledger/accounts/{aId}/balance with Tenant-B JWT → 404 (not 403).
///
/// Load-bearing rationale:
/// - If the EF Core global query filter is removed from ChassisDbContext, the Tenant-B
///   request returns 200 instead of 404, failing the IDOR assertion.
/// - If TenantMiddleware fails to hydrate the tenant context from the JWT, the EF filter
///   uses a null/wrong tenant, which either fails with 500 or returns the wrong data.
/// - This test exercises: JWT → TenantMiddleware → EF filter → 404 shape.
///
/// Docker dependency: requires Docker Engine for Testcontainers Postgres.
/// When Docker is unavailable this test throws during InitializeAsync and is reported
/// as errored (not failed) in CI. CI always has Docker.
/// </summary>
public sealed class LedgerIdorTests : IAsyncLifetime
{
    // Fixed GUIDs for reproducible assertions — no Guid.NewGuid() in test bodies.
    private static readonly Guid TenantA = new Guid("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("bbbbbbbb-2222-0000-0000-000000000002");
    private static readonly Guid UserA = new Guid("cccccccc-1111-0000-0000-000000000001");
    private static readonly Guid UserB = new Guid("dddddddd-2222-0000-0000-000000000002");

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    // The actual account ID generated during InitializeAsync seed — fields must appear
    // before methods per SA1201.
    private Guid _tenantAAccountId;
    private WebApplicationFactory<Program>? _factory;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        string connectionString = _postgres.GetConnectionString();

        _factory = new LedgerWebApplicationFactory(connectionString);

        // Ensure the ledger schema + tables exist via EF Core migration.
        using IServiceScope scope = _factory.Services.CreateScope();
        LedgerDbContext db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        await db.Database.MigrateAsync(CancellationToken.None);

        // Seed a pre-existing Account for Tenant A using a bypass scope so the
        // global query filter (which requires a tenant context) does not block the seed insert.
        ITenantContextAccessor accessor =
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        using IDisposable bypass = accessor.BeginBypass();

        Account tenantAAccount = Account.Create(TenantA, "Tenant-A Operating Account", "USD");
        db.Accounts.Add(tenantAAccount);
        await db.SaveChangesAsync(CancellationToken.None);

        _tenantAAccountId = tenantAAccount.Id;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Tenant-A POSTs a transaction to its own account → 201 Created.
    /// Tenant-B GETs the balance of Tenant-A's account → 404 (IDOR-resistant).
    ///
    /// A 403 (Forbidden) would reveal that the account exists; 404 is the required behaviour.
    /// </summary>
    [Fact]
    public async Task TenantB_CannotReadTenantA_AccountBalance_Returns404()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        // ── Step 1: Tenant-A POSTs a transaction → expects 201 ───────────────────────
        using HttpClient clientA = CreateClientFor(TenantA, UserA);

        var transactionPayload = new
        {
            Amount = 100.00m,
            Currency = "USD",
            Memo = "IDOR integration test",
            IdempotencyKey = (Guid?)new Guid("f0f0f0f0-0000-0000-0000-000000000001"),
        };

        HttpResponseMessage postResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/ledger/accounts/{_tenantAAccountId}/transactions",
            transactionPayload,
            CancellationToken.None);

        postResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "Tenant-A must be able to post a transaction to its own account");

        // ── Step 2: Tenant-B GETs the balance of Tenant-A's account → expects 404 ────
        // IDOR-resistance: the account exists in the DB but is invisible to Tenant-B
        // via the EF global query filter (and Postgres RLS in production).
        // Returning 403 would reveal the resource exists; 404 is the safe response.
        using HttpClient clientB = CreateClientFor(TenantB, UserB);

        HttpResponseMessage getResponse = await clientB.GetAsync(
            $"/api/v1/ledger/accounts/{_tenantAAccountId}/balance",
            CancellationToken.None);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "Tenant-B must receive 404 (not 403) when accessing Tenant-A's account — " +
                     "proving IDOR resistance via the EF global query filter");
    }

    private HttpClient CreateClientFor(Guid tenantId, Guid userId)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        string token = Phase2.ChassisHostFixture.MintToken(tenantId, userId);

        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// WebApplicationFactory that configures the Chassis host with a Testcontainers
    /// Postgres instance and overrides JWT validation to use the static test signing key.
    /// </summary>
    private sealed class LedgerWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public LedgerWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Both the Ledger module and the Identity module read this connection string.
                    ["ConnectionStrings:Chassis"] = _connectionString,
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Identity:SeedDevClient"] = "true",
                    ["Identity:AllowInsecureMetadata"] = "true",
                    ["Identity:Authority"] = Phase2.ChassisHostFixture.TestIssuer,
                    ["Identity:Audience"] = Phase2.ChassisHostFixture.TestAudience,
                    ["Identity:DevClient:Secret"] = "chassis-test-secret",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Replace JWT Bearer with the static test signing key so MintToken() tokens
                // are accepted without a live OpenIddict discovery endpoint.
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.BackchannelHttpHandler = null;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = Phase2.ChassisHostFixture.SigningKey,
                        ValidateIssuer = true,
                        ValidIssuer = Phase2.ChassisHostFixture.TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = Phase2.ChassisHostFixture.TestAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(5),
                        NameClaimType = "sub",
                        RoleClaimType = "roles",
                    };
                });
            });
        }
    }
}
