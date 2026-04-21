using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Tenancy;
using FluentAssertions;
using Ledger.Domain.Entities;
using Ledger.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Reporting.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Chassis.IntegrationTests.Phase4;

/// <summary>
/// Load-bearing outbox durability test.
///
/// Proves that events written to the EF Core outbox are eventually delivered to the
/// Reporting consumer even when RabbitMQ is temporarily unavailable.
///
/// Scenario:
/// 1. Publish a transaction event (writes to ledger outbox, broker UP).
/// 2. Verify outbox row exists in <c>ledger.OutboxMessage</c>.
/// 3. Stop RabbitMQ.
/// 4. Publish a second event (writes to outbox only — broker DOWN).
/// 5. Verify second outbox row exists.
/// 6. Restart RabbitMQ.
/// 7. Wait for outbox drain and assert both projections exist.
///
/// Load-bearing rationale:
/// - Without the EF Core outbox, event 2 would be lost when the broker is down.
/// - Without the consumer, outbox rows accumulate but projections never appear.
/// - This test exercises the full path: outbox write → broker → consumer → projection.
///
/// Docker dependency: requires Docker for both Postgres and RabbitMQ containers.
/// When Docker is unavailable the test errors (not fails) — same precedent as Phases 1–3.
/// </summary>
public sealed class OutboxDurabilityTests : IAsyncLifetime
{
    private static readonly Guid TenantId = new Guid("99999999-6666-0000-0000-000000000006");

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private readonly RabbitMqContainer _rabbit =
        new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();

    private WebApplicationFactory<Program>? _factory;

    // Populated during seed — used for the published event payloads.
    private Guid _accountId;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbit.StartAsync()).ConfigureAwait(false);

        _factory = new OutboxBusWebApplicationFactory(
            _postgres.GetConnectionString(),
            $"rabbitmq://{_rabbit.Hostname}:{_rabbit.GetMappedPublicPort(5672)}");

        using IServiceScope scope = _factory.Services.CreateScope();

        LedgerDbContext ledgerDb = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        await ledgerDb.Database.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

        ReportingDbContext reportingDb = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await reportingDb.Database.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

        // Seed a Ledger account using a bypass scope so the global query filter doesn't block.
        ITenantContextAccessor accessor =
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        using IDisposable bypass = accessor.BeginBypass();
        Account account = Account.Create(TenantId, "Outbox Test Account", "USD");
        ledgerDb.Accounts.Add(account);
        await ledgerDb.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        _accountId = account.Id;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbit.DisposeAsync().AsTask()).ConfigureAwait(false);
    }

    /// <summary>
    /// Posts two events with RabbitMQ stopped between them.
    /// After restarting the broker, both outbox messages must drain and both
    /// projection rows must appear.
    /// </summary>
    [Fact]
    public async Task Transaction_IsDelivered_AfterRabbitRestart()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        Guid txId1 = new Guid("b1b1b1b1-6666-0000-0000-000000000001");
        Guid txId2 = new Guid("b2b2b2b2-6666-0000-0000-000000000002");

        using IServiceScope scope = _factory.Services.CreateScope();
        IPublishEndpoint bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        LedgerDbContext ledgerDb = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        ReportingDbContext reportingDb = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        ITenantContextAccessor accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        accessor.Current = new TenantContext(TenantId, userId: null, correlationId: "outbox-test");

        // ── Step 1: Publish first event (broker UP) ───────────────────────────────
        await bus.Publish(new Ledger.Contracts.LedgerTransactionPosted(
            tenantId: TenantId,
            transactionId: txId1,
            accountId: _accountId,
            amount: 100.00m,
            currency: "USD",
            memo: "Outbox test tx1",
            occurredAt: DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);

        // ── Step 2: Verify outbox row written ─────────────────────────────────────
        using IDisposable bypass1 = accessor.BeginBypass();
        int outboxCount1 = await ledgerDb
            .Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>()
            .AsNoTracking()
            .CountAsync(CancellationToken.None)
            .ConfigureAwait(false);

        outboxCount1.Should().BeGreaterThanOrEqualTo(1,
            because: "first publish must write an outbox row");

        // ── Step 3: Stop RabbitMQ ─────────────────────────────────────────────────
        await _rabbit.StopAsync().ConfigureAwait(false);

        // ── Step 4: Publish second event (broker DOWN — write to outbox only) ──────
        await bus.Publish(new Ledger.Contracts.LedgerTransactionPosted(
            tenantId: TenantId,
            transactionId: txId2,
            accountId: _accountId,
            amount: 200.00m,
            currency: "USD",
            memo: "Outbox test tx2",
            occurredAt: DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);

        // ── Step 5: Verify second outbox row buffered ─────────────────────────────
        using IDisposable bypass2 = accessor.BeginBypass();
        int outboxCount2 = await ledgerDb
            .Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>()
            .AsNoTracking()
            .CountAsync(CancellationToken.None)
            .ConfigureAwait(false);

        outboxCount2.Should().BeGreaterThanOrEqualTo(2,
            because: "second publish must buffer an outbox row even with broker down");

        // ── Step 6: Restart RabbitMQ ──────────────────────────────────────────────
        await _rabbit.StartAsync().ConfigureAwait(false);

        // ── Step 7+8: Wait for outbox drain and projections to converge ───────────
        // Poll up to 30 seconds — BusOutboxDeliveryService polls every QueryDelay (1s).
        const int MaxWaitMs = 30_000;
        const int PollIntervalMs = 1_000;
        int elapsed = 0;
        int projectionCount = 0;

        while (elapsed < MaxWaitMs)
        {
            await Task.Delay(PollIntervalMs, CancellationToken.None).ConfigureAwait(false);
            elapsed += PollIntervalMs;

            using IDisposable bypass3 = accessor.BeginBypass();
            projectionCount = await reportingDb.TransactionProjections
                .AsNoTracking()
                .CountAsync(p => p.TenantId == TenantId, CancellationToken.None)
                .ConfigureAwait(false);

            if (projectionCount >= 2)
            {
                break;
            }
        }

        projectionCount.Should().Be(2,
            because: "both outbox messages must be delivered and projected after RabbitMQ restarts");
    }

    private sealed class OutboxBusWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly string _rabbitHost;

        public OutboxBusWebApplicationFactory(string connectionString, string rabbitHost)
        {
            _connectionString = connectionString;
            _rabbitHost = rabbitHost;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Chassis"] = _connectionString,
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Dispatch:Transport"] = "bus",
                    ["RabbitMq:Host"] = _rabbitHost,
                    ["RabbitMq:Username"] = "guest",
                    ["RabbitMq:Password"] = "guest",
                    ["Identity:Authority"] = Phase2.ChassisHostFixture.TestIssuer,
                    ["Identity:Audience"] = Phase2.ChassisHostFixture.TestAudience,
                    ["Identity:AllowInsecureMetadata"] = "true",
                    ["Identity:DevClient:Secret"] = "chassis-test-secret",
                });
            });

            builder.ConfigureTestServices(services =>
            {
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
                    };
                });
            });
        }
    }
}
