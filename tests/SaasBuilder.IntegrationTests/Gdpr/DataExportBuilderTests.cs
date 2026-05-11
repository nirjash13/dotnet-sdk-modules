using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gdpr.Application;
using Gdpr.Application.Abstractions;
using Xunit;

namespace SaasBuilder.IntegrationTests.Gdpr;

/// <summary>
/// Load-bearing tests for <see cref="DataExportBuilder"/>.
/// Verifies that the zip contains exactly the rows returned by registered providers,
/// and that providers not registered contribute nothing to the output.
/// </summary>
public sealed class DataExportBuilderTests
{
    private static readonly Guid TenantId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserId = new Guid("cccccccc-0000-0000-0000-000000000003");

    [Fact]
    public async Task BuildAsync_WhenProviderRegistered_ZipContainsProviderTable()
    {
        // Arrange
        var exportable = new FakeExportable("test_users", new[]
        {
            new Dictionary<string, object?> { ["id"] = "1", ["email"] = "user@example.com" },
        });

        var builder = new DataExportBuilder(new IExportable[] { exportable });

        // Act
        Stream zip = await builder.BuildAsync(TenantId, UserId, CancellationToken.None);

        // Assert — zip contains exactly one entry named "test_users.json"
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        archive.Entries.Should().ContainSingle(e => e.Name == "test_users.json");

        ZipArchiveEntry entry = archive.GetEntry("test_users.json")!;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        string json = await reader.ReadToEndAsync();
        var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json)!;
        rows.Should().ContainSingle();
        rows[0].Should().ContainKey("email");
    }

    [Fact]
    public async Task BuildAsync_WhenNoProvidersRegistered_ProducesEmptyZip()
    {
        // Arrange
        var builder = new DataExportBuilder(Array.Empty<IExportable>());

        // Act
        Stream zip = await builder.BuildAsync(TenantId, UserId, CancellationToken.None);

        // Assert
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        archive.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_MultipleProviders_EachProducesOwnEntry()
    {
        // Arrange
        var provider1 = new FakeExportable("profile", new[] { new Dictionary<string, object?> { ["name"] = "Alice" } });
        var provider2 = new FakeExportable("audit_logs", new[] { new Dictionary<string, object?> { ["action"] = "login" } });

        var builder = new DataExportBuilder(new IExportable[] { provider1, provider2 });

        // Act
        Stream zip = await builder.BuildAsync(TenantId, UserId, CancellationToken.None);

        // Assert — two separate JSON files, each containing their own data
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        archive.Entries.Should().HaveCount(2);
        archive.Entries.Should().Contain(e => e.Name == "profile.json");
        archive.Entries.Should().Contain(e => e.Name == "audit_logs.json");
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class FakeExportable : IExportable
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _rows;

        public FakeExportable(string tableName, IEnumerable<Dictionary<string, object?>> rows)
        {
            TableName = tableName;
            _rows = new List<IReadOnlyDictionary<string, object?>>(rows);
        }

        public string TableName { get; }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExportAsync(
            Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(_rows);
    }
}
