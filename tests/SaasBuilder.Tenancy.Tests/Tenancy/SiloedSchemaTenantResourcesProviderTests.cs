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
/// Load-bearing tests for <see cref="SiloedSchemaTenantResourcesProvider"/>.
/// Verifies conventional schema naming and per-tenant config override.
/// </summary>
public sealed class SiloedSchemaTenantResourcesProviderTests
{
    private const string DefaultCs = "Host=shared-db;Database=shared";
    private static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

    private static SiloedSchemaTenantResourcesProvider BuildProvider(
        Dictionary<string, string?> extraConfig)
    {
        var values = new Dictionary<string, string?>(extraConfig)
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultCs,
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new SiloedSchemaTenantResourcesProvider(
            config,
            NullLogger<SiloedSchemaTenantResourcesProvider>.Instance);
    }

    // ── Test 1: conventional schema name follows tenant_{tenantId:N} pattern ─────
    [Fact]
    public async Task GetAsync_WhenNoSchemaOverride_UsesConventionalSchemaName()
    {
        var provider = BuildProvider(new Dictionary<string, string?>());
        string expectedSchema = $"tenant_{TenantA:N}";

        ITenantResources resources = await provider.GetAsync(TenantA, CancellationToken.None);

        resources.Tags.Should().ContainKey("schema")
            .WhoseValue.Should().Be(expectedSchema);
    }

    // ── Test 2: per-tenant config override takes precedence over convention ───────
    [Fact]
    public async Task GetAsync_WhenSchemaOverrideConfigured_UsesOverriddenSchemaName()
    {
        const string customSchema = "acme_corp";
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"Tenancy:SiloedSchema:{TenantA:D}:Schema"] = customSchema,
        });

        ITenantResources resources = await provider.GetAsync(TenantA, CancellationToken.None);

        resources.Tags.Should().ContainKey("schema")
            .WhoseValue.Should().Be(customSchema);
    }

    // ── Test 3: connection string is the shared DefaultConnection ─────────────────
    [Fact]
    public async Task GetAsync_AlwaysReturnsSharedConnectionString()
    {
        var provider = BuildProvider(new Dictionary<string, string?>());

        ITenantResources resources = await provider.GetAsync(TenantA, CancellationToken.None);

        resources.ConnectionString.Should().Be(DefaultCs);
    }
}
