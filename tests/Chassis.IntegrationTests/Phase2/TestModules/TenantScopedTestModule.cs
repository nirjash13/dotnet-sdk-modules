using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Persistence;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chassis.IntegrationTests.Phase2.TestModules;

// ─────────────────────────────────────────────────────────────────────────────
// Test-only EF Core entity + DbContext
// These types exist ONLY to give the cross-tenant RLS E2E tests a real
// tenant-scoped table managed via EF Core + ChassisDbContext global filters.
// They are never shipped to production.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Minimal tenant-scoped entity used in cross-tenant RLS E2E tests.
/// Implements <see cref="ITenantScoped"/> so the <see cref="ChassisDbContext"/>
/// applies the global query filter automatically.
/// </summary>
public sealed class TestScopedItem : ITenantScoped
{
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid TenantId { get; set; }

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Minimal EF Core DbContext for the test module.
/// Inherits <see cref="ChassisDbContext"/> to get automatic tenant query filters.
/// </summary>
public sealed class TestItemDbContext(
    DbContextOptions<TestItemDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : ChassisDbContext(options, tenantContextAccessor)
{
    public DbSet<TestScopedItem> TestItems => Set<TestScopedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestScopedItem>(entity =>
        {
            entity.ToTable("test_scoped_items_e2e");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Value).HasMaxLength(200).IsRequired();
        });
    }
}

/// <summary>
/// Test-only <see cref="IModuleStartup"/> that:
/// 1. Registers <see cref="TestItemDbContext"/> against the test Postgres connection.
/// 2. Maps <c>POST /api/v1/test-items</c> (create) and <c>GET /api/v1/test-items/{id}</c> (fetch).
/// 3. Applied by <see cref="CrossTenantRlsE2EFixture"/> via
///    <c>ConfigureTestServices</c> rather than through the reflection module loader,
///    so it is invisible to the production host.
/// </summary>
public sealed class TenantScopedTestModule(string connectionString) : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddChassisPersistence<TestItemDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(TenantScopedTestModule).Assembly.FullName);
            });
        });
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/test-items")
            .RequireAuthorization()
            .WithTags("test-items");

        // POST /api/v1/test-items — create a new item for the current tenant.
        group.MapPost("/", CreateItem);

        // GET /api/v1/test-items/{id} — fetch a single item (RLS filters cross-tenant reads).
        group.MapGet("/{id:guid}", GetItem);

        // GET /api/v1/test-items — list items for the current tenant.
        group.MapGet("/", ListItems);
    }

    private static async Task<IResult> CreateItem(
        CreateTestItemRequest request,
        TestItemDbContext db,
        ITenantContextAccessor accessor,
        CancellationToken ct)
    {
        if (accessor.Current is null)
        {
            return Results.Unauthorized();
        }

        var item = new TestScopedItem
        {
            Id = request.Id ?? Guid.NewGuid(),
            TenantId = accessor.Current.TenantId,
            Value = request.Value,
        };

        db.TestItems.Add(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Results.Created($"/api/v1/test-items/{item.Id}", new { item.Id, item.Value });
    }

    private static async Task<IResult> GetItem(
        Guid id,
        TestItemDbContext db,
        CancellationToken ct)
    {
        // The global query filter on TestItemDbContext ensures only items for the current
        // tenant are visible. A cross-tenant read returns null → 404.
        TestScopedItem? item = await db.TestItems
            .AsNoTracking()
            .Where(e => e.Id == id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return item is null
            ? Results.NotFound()
            : Results.Ok(new { item.Id, item.Value, item.TenantId });
    }

    private static async Task<IResult> ListItems(
        TestItemDbContext db,
        CancellationToken ct)
    {
        List<object> items = await db.TestItems
            .AsNoTracking()
            .Select(e => new { e.Id, e.Value, e.TenantId } as object)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(items);
    }
}

/// <summary>
/// Request body for <c>POST /api/v1/test-items</c>.
/// </summary>
public sealed class CreateTestItemRequest
{
    /// <summary>Optional explicit item ID — use a deterministic GUID in tests.</summary>
    public Guid? Id { get; set; }

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Helper hosted service that creates the <see cref="TestItemDbContext"/> schema
/// (EnsureCreated) on startup. Used ONLY in test environments.
/// </summary>
public sealed class TestSchemaInitialiser(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        TestItemDbContext db = scope.ServiceProvider.GetRequiredService<TestItemDbContext>();

        // EnsureCreated is appropriate here: this is a test-only in-memory migration
        // with a throw-away container. Never use EnsureCreated in production.
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
