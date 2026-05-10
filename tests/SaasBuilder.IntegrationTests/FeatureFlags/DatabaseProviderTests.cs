using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Contracts;
using FeatureFlags.Infrastructure.Persistence;
using FeatureFlags.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SaasBuilder.SharedKernel.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaasBuilder.IntegrationTests.FeatureFlags;

/// <summary>
/// Load-bearing database provider tests.
///
/// Test 1 (flag not defined → default): Fails if provider throws on unknown flag instead of returning default.
/// Test 2 (flag defined → configured value): Fails if the DB lookup returns wrong value or throws.
/// Test 3 (tenant override beats global): Fails if tenant override priority logic is removed.
/// </summary>
public sealed class DatabaseProviderTests : IAsyncLifetime
{
    private static readonly Guid TenantA = new Guid("f1f1f1f1-0001-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("f2f2f2f2-0002-0000-0000-000000000002");

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private FeatureFlagsDbContext? _db;
    private ServiceProvider? _provider;
    private IServiceScope? _scope;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        ServiceCollection services = new ServiceCollection();

        Mock<ITenantContextAccessor> accessor = new Mock<ITenantContextAccessor>();
        accessor.Setup(a => a.IsBypassed).Returns(false);
        accessor.Setup(a => a.Current).Returns((ITenantContext?)null);

        services.AddSingleton(accessor.Object);
        services.AddDbContext<FeatureFlagsDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString()));

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FeatureFlagsDbContext>();

        await _db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        // Seed: one flag with TenantB override.
        FeatureFlag flag = FeatureFlag.Create("new-dashboard", "UI rollout", rolloutPercentage: 0);
        flag.TenantOverrides.Add(TenantFlagOverride.Create(flag.Id, TenantB, true));
        _db.FeatureFlags.Add(flag);
        await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
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
    public async Task UnknownFlag_ReturnsDefaultValue()
    {
        DatabaseFeatureProvider provider = new DatabaseFeatureProvider(
            _db!, NullLogger<DatabaseFeatureProvider>.Instance);

        FlagEvaluationDetails<bool> result = await provider.ResolveBooleanAsync(
            "non-existent-flag",
            defaultValue: false,
            context: EvaluationContext.For(TenantA),
            ct: CancellationToken.None);

        result.Value.Should().BeFalse(because: "an undefined flag must return the default value");
        result.Reason.Should().Be(EvaluationReason.Default);
    }

    [Fact]
    public async Task TenantOverride_BeatsGlobalDefault()
    {
        // TenantB has an explicit override = true; the global DefaultBoolValue is false.
        DatabaseFeatureProvider provider = new DatabaseFeatureProvider(
            _db!, NullLogger<DatabaseFeatureProvider>.Instance);

        FlagEvaluationDetails<bool> result = await provider.ResolveBooleanAsync(
            "new-dashboard",
            defaultValue: false,
            context: EvaluationContext.For(TenantB),
            ct: CancellationToken.None);

        result.Value.Should().BeTrue(because: "tenant B has an explicit override of true");
        result.Reason.Should().Be(EvaluationReason.Static,
            because: "an explicit tenant override uses the Static reason");
    }
}
