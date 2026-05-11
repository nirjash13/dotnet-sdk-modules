using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaasBuilder.Persistence.Tenancy;
using SaasBuilder.SharedKernel.Tenancy;
using Xunit;

namespace SaasBuilder.Tenancy.Tests.Tenancy;

/// <summary>
/// Load-bearing tests for <see cref="SiloedDatabaseTenantResourcesProvider"/>.
/// Verifies config-based connection-string resolution and fallback behaviour.
/// </summary>
public sealed class SiloedDatabaseTenantResourcesProviderTests
{
    private static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

    private static SiloedDatabaseTenantResourcesProvider BuildProvider(
        Dictionary<string, string?> configValues)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new SiloedDatabaseTenantResourcesProvider(
            config,
            NullLogger<SiloedDatabaseTenantResourcesProvider>.Instance);
    }

    // ── Test 1: per-tenant connection string is returned when configured ─────────
    [Fact]
    public async Task GetAsync_WhenPerTenantKeyExists_ReturnsPerTenantConnectionString()
    {
        const string perTenantCs = "Host=acme-db;Database=acme";
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"Tenancy:SiloedDatabase:{TenantA:D}:ConnectionString"] = perTenantCs,
        });

        ITenantResources resources = await provider.GetAsync(TenantA, CancellationToken.None);

        resources.ConnectionString.Should().Be(perTenantCs);
    }

    // ── Test 2: falls back to DefaultConnection when no per-tenant key ───────────
    [Fact]
    public async Task GetAsync_WhenPerTenantKeyMissing_FallsBackToDefaultConnection()
    {
        const string defaultCs = "Host=shared-db;Database=shared";
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = defaultCs,
        });

        ITenantResources resources = await provider.GetAsync(TenantA, CancellationToken.None);

        resources.ConnectionString.Should().Be(defaultCs);
    }

    // ── Test 3: throws when neither per-tenant key nor DefaultConnection exists ───
    [Fact]
    public async Task GetAsync_WhenNoConfigExists_ThrowsInvalidOperationException()
    {
        var provider = BuildProvider(new Dictionary<string, string?>());

        Func<Task> act = () => provider.GetAsync(TenantA, CancellationToken.None).AsTask();

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*SiloedDatabase*");
    }
}
