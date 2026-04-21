using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Host;
using Chassis.Host.Modules;
using Chassis.IntegrationTests.Phase2.TestModules;
using Chassis.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Chassis.IntegrationTests.Phase2;

/// <summary>
/// Acceptance criterion (a): token issued for Tenant-A cannot read Tenant-B data —
/// end-to-end RLS proof across HTTP → TenantMiddleware → EF Core global filter → Postgres.
///
/// Load-bearing rationale:
/// - If <see cref="Chassis.Persistence.ChassisDbContext"/>'s global query filter is removed,
///   the cross-tenant read returns 200 instead of 404.
/// - If TenantMiddleware fails to propagate the JWT tenant_id claim to
///   ITenantContextAccessor, the EF filter uses the wrong (or null) tenant.
/// - Both bugs are caught by distinct test assertions below.
/// </summary>
public sealed class CrossTenantRlsE2ETests : IAsyncLifetime
{
    private static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid UserA = new Guid("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid UserB = new Guid("dddddddd-0000-0000-0000-000000000004");

    // Deterministic item IDs so assertions can reference them by ID.
    private static readonly Guid ItemAId = new Guid("11111111-0000-0000-0000-000000000001");
    private static readonly Guid ItemBId = new Guid("22222222-0000-0000-0000-000000000002");

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private WebApplicationFactory<Program>? _factory;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);
        string connectionString = _postgres.GetConnectionString();

        var testModule = new TenantScopedTestModule(connectionString);
        _factory = new RlsWebApplicationFactory(connectionString, testModule);

        // Seed one item per tenant using their respective tokens.
        await SeedItemAsync(TenantA, UserA, ItemAId, "value-for-tenant-a").ConfigureAwait(false);
        await SeedItemAsync(TenantB, UserB, ItemBId, "value-for-tenant-b").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    // ── Test (a-1): tenant A can read its own item ────────────────────────────────
    [Fact]
    public async Task TenantA_CanReadOwnItem_Returns200WithBody()
    {
        using HttpClient client = CreateClientFor(TenantA, UserA);

        HttpResponseMessage response = await client.GetAsync($"/api/v1/test-items/{ItemAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Tenant A must be able to read its own item");

        TestItemResponse? body = await response.Content
            .ReadFromJsonAsync<TestItemResponse>(CancellationToken.None);

        body.Should().NotBeNull();
        body!.Id.Should().Be(ItemAId);
        body.Value.Should().Be("value-for-tenant-a");
    }

    // ── Test (a-2): tenant A cannot read tenant B's item — 404 not 403 ───────────
    // 404 (not 403) per IMPLEMENTATION_PLAN.md §Phase 3 line 424 — IDOR resistance:
    // never reveal the existence of another tenant's resources.
    [Fact]
    public async Task TenantA_CannotReadTenantBItem_Returns404()
    {
        using HttpClient client = CreateClientFor(TenantA, UserA);

        HttpResponseMessage response = await client.GetAsync($"/api/v1/test-items/{ItemBId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "the EF Core global query filter must hide Tenant B's item from Tenant A " +
                     "and return 404 (not 403) to prevent IDOR attacks");
    }

    // ── Test (a-3): list operation returns only caller's own items ─────────────────
    [Fact]
    public async Task TenantA_ListItems_ReturnsOnlyTenantAItems()
    {
        using HttpClient client = CreateClientFor(TenantA, UserA);

        HttpResponseMessage response = await client.GetAsync("/api/v1/test-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<TestItemResponse>? items = await response.Content
            .ReadFromJsonAsync<List<TestItemResponse>>(CancellationToken.None);

        items.Should().NotBeNull();
        items!.Should().HaveCount(1,
            because: "the global query filter must return only Tenant A's one item");

        items[0].TenantId.Should().Be(TenantA,
            because: "every item in the list must belong to Tenant A");
    }

    private HttpClient CreateClientFor(Guid tenantId, Guid userId)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        string token = ChassisHostFixture.MintToken(tenantId, userId);

        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SeedItemAsync(Guid tenantId, Guid userId, Guid itemId, string value)
    {
        using HttpClient client = CreateClientFor(tenantId, userId);

        var payload = new { Id = itemId, Value = value };
        HttpResponseMessage response = await client
            .PostAsJsonAsync("/api/v1/test-items", payload, CancellationToken.None)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Factory that injects the test module by finding the registered IModuleLoader singleton
    /// instance and replacing it with a decorator that also includes the test module.
    /// This causes <c>UseChassisPipeline</c> to call <c>testModule.Configure(endpoints)</c>
    /// during endpoint registration — no framework hacks needed.
    /// </summary>
    private sealed class RlsWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly TenantScopedTestModule _testModule;

        public RlsWebApplicationFactory(string connectionString, TenantScopedTestModule testModule)
        {
            _connectionString = connectionString;
            _testModule = testModule;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Identity:SeedDevClient"] = "true",
                    ["Identity:AllowInsecureMetadata"] = "true",
                    ["Identity:Authority"] = ChassisHostFixture.TestIssuer,
                    ["Identity:Audience"] = ChassisHostFixture.TestAudience,
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // 1. Register test module's DbContext + persistence infrastructure.
                _testModule.ConfigureServices(services, new ConfigurationBuilder().Build());

                // 2. Schema initialiser creates the test table on startup.
                services.AddHostedService<TestSchemaInitialiser>();

                // 3. Replace IModuleLoader with a decorator that appends the test module.
                //    Find the original IModuleLoader singleton descriptor (registered as an
                //    instance by AddChassisHost), wrap it, and re-register.
                ServiceDescriptor? original = services
                    .FirstOrDefault(d => d.ServiceType == typeof(IModuleLoader));

                if (original?.ImplementationInstance is IModuleLoader originalLoader)
                {
                    services.Remove(original);
                    services.AddSingleton<IModuleLoader>(
                        new TestModuleLoaderDecorator(originalLoader, _testModule));
                }

                // 4. Replace JWT Bearer with the static test signing key.
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.BackchannelHttpHandler = null;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = ChassisHostFixture.SigningKey,
                        ValidateIssuer = true,
                        ValidIssuer = ChassisHostFixture.TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = ChassisHostFixture.TestAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(5),
                        NameClaimType = "sub",
                        RoleClaimType = "roles",
                    };
                });
            });
        }
    }

    /// <summary>Minimal response shape for test assertions.</summary>
    private sealed class TestItemResponse
    {
        public Guid Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
    }
}

/// <summary>
/// IModuleLoader decorator that appends a test module to the original loader's results.
/// Used only in test fixtures — not registered in production.
/// </summary>
internal sealed class TestModuleLoaderDecorator : IModuleLoader
{
    private readonly IModuleLoader _inner;
    private readonly IModuleStartup _testModule;

    public TestModuleLoaderDecorator(IModuleLoader inner, IModuleStartup testModule)
    {
        _inner = inner;
        _testModule = testModule;
    }

    public IReadOnlyList<ModuleLoadDiagnostic> LoadDiagnostics => _inner.LoadDiagnostics;

    public IEnumerable<IModuleStartup> Load()
    {
        foreach (IModuleStartup module in _inner.Load())
        {
            yield return module;
        }

        yield return _testModule;
    }
}
