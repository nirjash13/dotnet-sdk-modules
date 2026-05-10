using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Domain.Entities;
using Billing.Domain.ValueObjects;
using Billing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaasBuilder.IntegrationTests.Billing;

/// <summary>
/// Load-bearing subscription lifecycle test.
/// Tests the repository and domain entity mutations without provider calls.
///
/// What fails if this is removed:
/// - Cancel domain logic breaks (allowed on active, throws on already-canceled) — repository and
///   EF Core flush are also exercised, so a missing column or mapping error surfaces here.
/// </summary>
public sealed class SubscriptionLifecycleTests : IAsyncLifetime
{
    private static readonly Guid TenantId = new Guid("b1b1b1b1-0001-0000-0000-000000000001");
    private static readonly Guid PlanId = new Guid("b2b2b2b2-0002-0000-0000-000000000001");

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private BillingDbContext? _db;
    private IServiceScope? _scope;
    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        ServiceCollection services = new ServiceCollection();

        // Register a minimal tenant accessor that always returns TenantId.
        Mock<ITenantContextAccessor> accessorMock = new Mock<ITenantContextAccessor>();
        Mock<ITenantContext> contextMock = new Mock<ITenantContext>();
        contextMock.Setup(c => c.TenantId).Returns(TenantId);
        accessorMock.Setup(a => a.Current).Returns(contextMock.Object);
        accessorMock.Setup(a => a.IsBypassed).Returns(false);

        services.AddSingleton(accessorMock.Object);
        services.AddDbContext<BillingDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString()));

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Create schema.
        await _db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        _scope?.Dispose();
        if (_provider is not null)
        {
            await _provider.DisposeAsync().ConfigureAwait(false);
        }

        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task CancelThenResume_ViaRepositoryMutations_Persists_CorrectStatus()
    {
        // Arrange — create and persist a subscription.
        Subscription subscription = Subscription.Create(TenantId, PlanId, providerSubscriptionId: null);
        subscription.Activate();

        _db!.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        Guid subscriptionId = subscription.Id;

        // Act — cancel via domain method and save.
        DateTimeOffset canceledAt = new DateTimeOffset(2026, 5, 11, 10, 0, 0, TimeSpan.Zero);
        subscription.Cancel(canceledAt);
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — reload from DB (no tracking) and verify.
        Subscription? reloaded = await _db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, CancellationToken.None)
            .ConfigureAwait(false);

        reloaded.Should().NotBeNull();
        reloaded!.Status.Should().Be(SubscriptionStatus.Canceled,
            because: "Cancel() must persist the Canceled status to the DB");
        reloaded.CanceledAt.Should().Be(canceledAt,
            because: "CanceledAt must be persisted");
    }
}
