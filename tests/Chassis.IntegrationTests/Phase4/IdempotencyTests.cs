using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ledger.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Reporting.Application.Consumers;
using Reporting.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Chassis.IntegrationTests.Phase4;

/// <summary>
/// Load-bearing idempotency test for the Reporting consumer.
///
/// Proves that calling Consume with the same MessageId three times
/// results in exactly ONE <see cref="Reporting.Application.Persistence.TransactionProjection"/> row.
///
/// Load-bearing rationale:
/// - If the unique index on (TenantId, SourceMessageId) is missing, all three calls insert rows
///   and the count is 3, failing the assertion.
/// - If the consumer's ExistsAsync check is removed but the index exists, the third insert
///   collides and the consumer throws PostgresException(23505) — still a broken test.
/// - This test exercises the consumer + DB idempotency layer directly.
/// </summary>
public sealed class IdempotencyTests : IAsyncLifetime
{
    private static readonly Guid TenantId = new Guid("eeeeeeee-4444-0000-0000-000000000004");
    private static readonly Guid AccountId = new Guid("aaaaaaaa-4444-0000-0000-000000000001");
    private static readonly Guid FixedMessageId = new Guid("f1f1f1f1-4444-0000-0000-000000000001");
    private static readonly Guid TransactionId = new Guid("c0c0c0c0-4444-0000-0000-000000000001");

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);
        _factory = new IdempotencyWebApplicationFactory(_postgres.GetConnectionString());

        using IServiceScope scope = _factory.Services.CreateScope();
        ReportingDbContext db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await db.Database.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Publishing the same <see cref="LedgerTransactionPosted"/> three times with the same
    /// MessageId must produce exactly ONE projection row.
    /// </summary>
    [Fact]
    public async Task SameMessage_PublishedThreeTimes_ProducesExactlyOneProjectionRow()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        LedgerTransactionPosted message = new LedgerTransactionPosted(
            tenantId: TenantId,
            transactionId: TransactionId,
            accountId: AccountId,
            amount: 250.00m,
            currency: "EUR",
            memo: "Idempotency test",
            occurredAt: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        using IServiceScope scope = _factory.Services.CreateScope();
        LedgerTransactionPostedConsumer consumer =
            scope.ServiceProvider.GetRequiredService<LedgerTransactionPostedConsumer>();
        ReportingDbContext db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        // Invoke Consume three times with the same MessageId via a Moq mock.
        for (int i = 0; i < 3; i++)
        {
            Mock<ConsumeContext<LedgerTransactionPosted>> ctx =
                new Mock<ConsumeContext<LedgerTransactionPosted>>();

            ctx.Setup(c => c.Message).Returns(message);
            ctx.Setup(c => c.MessageId).Returns(FixedMessageId);
            ctx.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

            await consumer.Consume(ctx.Object).ConfigureAwait(false);
        }

        int count = await db.TransactionProjections
            .AsNoTracking()
            .CountAsync(
                p => p.TenantId == TenantId && p.SourceMessageId == FixedMessageId,
                CancellationToken.None)
            .ConfigureAwait(false);

        count.Should().Be(1,
            because: "duplicate message deliveries must be deduplicated by the consumer + unique index");
    }

    private sealed class IdempotencyWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public IdempotencyWebApplicationFactory(string connectionString)
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
                    ["ConnectionStrings:Chassis"] = _connectionString,
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Dispatch:Transport"] = "inproc",
                    ["Identity:Authority"] = Phase2.ChassisHostFixture.TestIssuer,
                    ["Identity:Audience"] = Phase2.ChassisHostFixture.TestAudience,
                    ["Identity:AllowInsecureMetadata"] = "true",
                    ["Identity:DevClient:Secret"] = "chassis-test-secret",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Register the consumer directly so it can be resolved from the test scope.
                services.AddScoped<LedgerTransactionPostedConsumer>();

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
